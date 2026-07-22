using RimForge.Analysis.Models;
using RimForge.Core.Models;

namespace RimForge.Analysis.Services;

public sealed class IssueEngine
{
    public IssueViewerSnapshot Build(
        ModAnalysisSnapshot analysis,
        IssueScopeKind scope,
        string scopeName,
        IReadOnlyCollection<ModRecord> mods,
        IReadOnlyCollection<string>? activePackageIds = null,
        IReadOnlySet<string>? ignoredIssueIds = null)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(mods);

        var names = mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
            .GroupBy(mod => mod.PackageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().DisplayName, StringComparer.OrdinalIgnoreCase);

        string NameOf(string? packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId)) return "Unknown mod";
            if (names.TryGetValue(packageId, out var name) && !string.IsNullOrWhiteSpace(name)) return name;
            var fallback = packageId.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? packageId;
            return fallback.Replace('_', ' ').Replace('-', ' ');
        }

        var packageScope = scope == IssueScopeKind.ActiveProfile
            ? new HashSet<string>(activePackageIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
            : null;

        var issues = analysis.Issues
            .Where(issue => packageScope is null || packageScope.Contains(issue.PackageId))
            .Select(issue => ToWorkItem(issue, NameOf, ignoredIssueIds))
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ModName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var summary = new IssueScopeSummary(
            scope,
            string.IsNullOrWhiteSpace(scopeName) ? "No Active Profile" : scopeName,
            issues.Count(item => !item.IsIgnored && item.Severity == AnalysisIssueSeverity.Error),
            issues.Count(item => !item.IsIgnored && item.Severity == AnalysisIssueSeverity.Warning),
            issues.Count(item => !item.IsIgnored && item.Severity == AnalysisIssueSeverity.Information),
            issues.Select(item => item.PackageId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            issues.Length);

        return new IssueViewerSnapshot(summary, issues);
    }

    private static IssueWorkItem ToWorkItem(ModAnalysisIssue issue, Func<string?, string> nameOf, IReadOnlySet<string>? ignoredIssueIds)
    {
        var (action, resolution, canAutoFix, category, recommendation, why) = issue.Code switch
        {
            AnalysisIssueCode.MissingRequiredDependency => (
                RepairActionKind.InstallDependency,
                IssueResolutionKind.Assisted,
                false,
                "Missing Mods",
                "Subscribe to or enable the missing dependency, then refresh and validate the profile.",
                "A required dependency is unavailable, so this mod may fail to load or behave incorrectly."),
            AnalysisIssueCode.InactiveRequiredDependency => (
                RepairActionKind.ActivateDependency,
                IssueResolutionKind.Assisted,
                false,
                "Inactive Dependencies",
                "Enable the installed dependency in the active profile, then recalculate the load order.",
                "The dependency exists in the installed library but is not active, so the dependent mod cannot load safely."),
            AnalysisIssueCode.LoadOrderViolation => (
                RepairActionKind.ReorderProfile,
                IssueResolutionKind.Automatic,
                true,
                "Load Order",
                "Apply the recommended load order for the active profile.",
                "Incorrect ordering can cause patches, definitions, or assemblies to initialize in the wrong sequence."),
            AnalysisIssueCode.DuplicatePackageId => (
                RepairActionKind.DisableDuplicate,
                IssueResolutionKind.Assisted,
                false,
                "Installation",
                "Keep one authoritative installation and disable or remove the duplicate.",
                "Duplicate package IDs make mod resolution ambiguous and can load the wrong files."),
            AnalysisIssueCode.DependencyCycle => (
                RepairActionKind.ReviewCycle,
                IssueResolutionKind.Manual,
                false,
                "Dependencies",
                "Choose which mod should load first, then let RimForge rebuild and validate the resulting order.",
                "A dependency cycle prevents RimForge from establishing a deterministic load order."),
            AnalysisIssueCode.LoadOrderBlockedByCycle => (
                RepairActionKind.ReviewCycle,
                IssueResolutionKind.Manual,
                false,
                "Load Order",
                "Resolve the related dependency cycle before placing this mod.",
                "This mod cannot be positioned until the upstream cycle has been resolved."),
            AnalysisIssueCode.CuratedRuleConflict => (
                RepairActionKind.InspectMetadata,
                IssueResolutionKind.Manual,
                false,
                "Curated Rules",
                "Review the conflicting or invalid curated records before enabling them again.",
                "RimForge quarantines contradictory or invalid curated knowledge instead of silently choosing a rule."),
            AnalysisIssueCode.ReplacementRecommended => (
                RepairActionKind.InspectMetadata,
                IssueResolutionKind.Manual,
                false,
                "Use This Instead",
                "Compare the installed mod with the recommended maintained replacement. No subscription or removal is performed automatically.",
                "A curated advisory record indicates that a maintained or safer replacement may be available."),
            AnalysisIssueCode.CompatibilityEvidenceConcern => (
                RepairActionKind.InspectMetadata,
                IssueResolutionKind.Assisted,
                false,
                "Compatibility Evidence",
                "Inspect both mods and the supporting evidence before changing the active profile.",
                "Unified evidence indicates a declared or observed compatibility concern between installed mods."),
            AnalysisIssueCode.RuntimeObservedConflict => (
                RepairActionKind.InspectMetadata,
                IssueResolutionKind.Assisted,
                false,
                "Runtime Evidence",
                "Inspect the affected mods, session evidence, and available compatibility patches.",
                "The Runtime Sensor observed this conflict during actual game execution."),
            AnalysisIssueCode.RuntimePerformanceRegression => (
                RepairActionKind.InspectMetadata,
                IssueResolutionKind.Assisted,
                false,
                "Runtime Performance",
                "Inspect the observed workload, affected mods, and performance evidence before changing the profile.",
                "Runtime measurements indicate a repeatable performance regression."),
            AnalysisIssueCode.RuntimeIntegrationFailure => (
                RepairActionKind.InspectMetadata,
                IssueResolutionKind.Assisted,
                false,
                "Runtime Integration",
                "Inspect the exception or integration evidence and the affected mods.",
                "The Runtime Sensor observed an integration failure during actual game execution."),
            _ => (
                RepairActionKind.InspectMetadata,
                IssueResolutionKind.Manual,
                false,
                "Metadata",
                "Inspect the mod metadata and correct the reported issue.",
                "Invalid or incomplete metadata prevents reliable identification and analysis."),
        };

        var issueId = string.IsNullOrWhiteSpace(issue.SourceIdentity)
            ? $"{issue.PackageId}:{issue.Code}"
            : $"{issue.PackageId}:{issue.Code}:{issue.SourceIdentity}";
        return new IssueWorkItem(
            issueId,
            issue.PackageId,
            nameOf(issue.PackageId),
            issue.Code,
            issue.Severity,
            category,
            issue.Title,
            issue.Explanation,
            why,
            recommendation,
            action,
            resolution,
            canAutoFix,
            ignoredIssueIds?.Contains(issueId) == true,
            issue.RelatedPackageIds,
            issue.RelatedPackageIds.Select(nameOf).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
