using RimForge.Core.Models;

namespace RimForge.Analysis.Models;

public enum AnalysisRelationshipKind
{
    RequiredDependency,
    LoadBefore,
    LoadAfter,
    ObservedConflict
}

public enum AnalysisIssueSeverity
{
    Information,
    Warning,
    Error
}

public enum AnalysisIssueCode
{
    MissingPackageId,
    DuplicatePackageId,
    MissingRequiredDependency,
    InactiveRequiredDependency,
    DependencyCycle,
    LoadOrderBlockedByCycle,
    LoadOrderViolation,
    CuratedRuleConflict,
    ReplacementRecommended,
    CompatibilityEvidenceConcern,
    UserLockConflict,
    RuntimeObservedConflict,
    RuntimePerformanceRegression,
    RuntimeIntegrationFailure
}

public sealed record AnalysisRelationship(
    string SourcePackageId,
    string TargetPackageId,
    AnalysisRelationshipKind Kind,
    string Explanation,
    LoadOrderRuleConfidence Confidence = LoadOrderRuleConfidence.Hard,
    string RuleSource = "Mod metadata",
    bool IsMandatory = true);

public sealed record ModAnalysisIssue(
    AnalysisIssueCode Code,
    AnalysisIssueSeverity Severity,
    string PackageId,
    string Title,
    string Explanation,
    IReadOnlyList<string> RelatedPackageIds,
    string? SourceIdentity = null);

public sealed record ModAnalysisSummary(
    string PackageId,
    int RequiredDependencyCount,
    int DirectDependentCount,
    int TransitiveDependentCount,
    int IssueCount,
    bool IsInCycle,
    string HealthLabel,
    string ImpactLabel);

public sealed record DisableImpact(
    string PackageId,
    bool CanDisableSafely,
    IReadOnlyList<string> DirectDependents,
    IReadOnlyList<string> TransitiveDependents,
    string Explanation);


public enum RepairActionKind
{
    InstallDependency,
    ActivateDependency,
    DisableDuplicate,
    ReorderProfile,
    ReviewCycle,
    InspectMetadata
}

public sealed record RepairRecommendation(
    RepairActionKind Kind,
    AnalysisIssueSeverity Severity,
    string PackageId,
    string Title,
    string Explanation,
    IReadOnlyList<string> RelatedPackageIds);

public sealed record ProfileHealthScore(
    int Score,
    string Label,
    int ErrorCount,
    int WarningCount,
    int InformationCount,
    string Explanation);

public sealed record LoadOrderDecision(
    string PackageId,
    int PreviousIndex,
    int ProposedIndex,
    string PrimaryReason,
    string RuleSource,
    LoadOrderRuleConfidence Confidence,
    bool IsRequired,
    IReadOnlyList<string> RelatedPackageIds)
{
    public bool WasMoved => PreviousIndex >= 0 && ProposedIndex >= 0 && PreviousIndex != ProposedIndex;
}

public sealed record TopologicalOrderResult(
    bool IsComplete,
    IReadOnlyList<string> OrderedPackageIds,
    IReadOnlyList<string> BlockedPackageIds,
    string Explanation)
{
    public IReadOnlyList<LoadOrderDecision> Decisions { get; init; } = Array.Empty<LoadOrderDecision>();
}

public sealed record LoadOrderEntry(string ModName, string PackageId);

public sealed record LoadOrderPlan(
    bool IsComplete,
    IReadOnlyList<LoadOrderEntry> OrderedMods,
    IReadOnlyList<LoadOrderEntry> BlockedMods,
    IReadOnlyList<IReadOnlyList<LoadOrderEntry>> CycleGroups,
    string Explanation)
{
    public IReadOnlyList<string> OrderedPackageIds => OrderedMods.Select(mod => mod.PackageId).ToArray();
    public IReadOnlyList<string> BlockedPackageIds => BlockedMods.Select(mod => mod.PackageId).ToArray();
}

public sealed class ModAnalysisSnapshot
{
    private readonly IReadOnlyDictionary<string, ModRecord> _mods;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _dependencies;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _dependents;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ModAnalysisIssue>> _issues;
    private readonly IReadOnlySet<string> _cycleMembers;

    public ModAnalysisSnapshot(
        IReadOnlyDictionary<string, ModRecord> mods,
        IReadOnlyList<AnalysisRelationship> relationships,
        IReadOnlyList<ModAnalysisIssue> issues,
        IReadOnlyList<IReadOnlyList<string>> cycles,
        IReadOnlyDictionary<string, IReadOnlyList<string>> dependencies,
        IReadOnlyDictionary<string, IReadOnlyList<string>> dependents,
        TopologicalOrderResult proposedOrder,
        LoadOrderPlan loadOrderPlan)
    {
        _mods = mods;
        Relationships = relationships;
        Issues = issues;
        Cycles = cycles;
        _dependencies = dependencies;
        _dependents = dependents;
        ProposedOrder = proposedOrder;
        LoadOrderPlan = loadOrderPlan;
        _issues = issues
            .GroupBy(issue => issue.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ModAnalysisIssue>)group.ToArray(), StringComparer.OrdinalIgnoreCase);
        _cycleMembers = cycles.SelectMany(cycle => cycle).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<AnalysisRelationship> Relationships { get; }
    public IReadOnlyList<ModAnalysisIssue> Issues { get; }
    public IReadOnlyList<IReadOnlyList<string>> Cycles { get; }
    public TopologicalOrderResult ProposedOrder { get; }
    public LoadOrderPlan LoadOrderPlan { get; }
    public int ModCount => _mods.Count;

    public IReadOnlyList<ModAnalysisIssue> GetIssues(string? packageId) =>
        packageId is not null && _issues.TryGetValue(packageId, out var issues)
            ? issues
            : Array.Empty<ModAnalysisIssue>();

    public IReadOnlyList<string> GetDependencies(string? packageId) =>
        packageId is not null && _dependencies.TryGetValue(packageId, out var values)
            ? values
            : Array.Empty<string>();

    public IReadOnlyList<string> GetDependents(string? packageId) =>
        packageId is not null && _dependents.TryGetValue(packageId, out var values)
            ? values
            : Array.Empty<string>();

    public IReadOnlyList<string> GetTransitiveDependents(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return Array.Empty<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>(GetDependents(packageId));
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!visited.Add(current)) continue;
            foreach (var dependent in GetDependents(current)) pending.Enqueue(dependent);
        }
        visited.Remove(packageId);
        return visited.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public DisableImpact GetDisableImpact(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            return new DisableImpact(string.Empty, false, Array.Empty<string>(), Array.Empty<string>(), "No mod is selected.");

        var direct = GetDependents(packageId);
        var transitive = GetTransitiveDependents(packageId);
        return new DisableImpact(
            packageId,
            direct.Count == 0,
            direct,
            transitive,
            direct.Count == 0
                ? "No installed mods require this mod."
                : $"Disabling this mod would affect {transitive.Count} dependent mod(s), including {direct.Count} direct dependent(s).");
    }

    public ProfileHealthScore GetProfileHealth(IReadOnlyCollection<string>? activePackageIds)
    {
        var scopedIssues = activePackageIds is null
            ? Issues
            : Issues.Where(issue => activePackageIds.Contains(issue.PackageId, StringComparer.OrdinalIgnoreCase)).ToArray();
        var errors = scopedIssues.Count(issue => issue.Severity == AnalysisIssueSeverity.Error);
        var warnings = scopedIssues.Count(issue => issue.Severity == AnalysisIssueSeverity.Warning);
        var information = scopedIssues.Count(issue => issue.Severity == AnalysisIssueSeverity.Information);
        var score = Math.Clamp(100 - (errors * 12) - (warnings * 4) - information, 0, 100);
        var label = score >= 95 ? "Excellent" : score >= 85 ? "Healthy" : score >= 70 ? "Needs attention" : score >= 50 ? "At risk" : "Critical";
        return new ProfileHealthScore(
            score, label, errors, warnings, information,
            scopedIssues.Count == 0
                ? "No dependency or load-order issues were detected."
                : $"{errors} error(s), {warnings} warning(s), and {information} informational issue(s) reduced this profile's health score.");
    }

    public IReadOnlyList<RepairRecommendation> GetRecommendations(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return Array.Empty<RepairRecommendation>();
        return GetIssues(packageId).Select(issue => issue.Code switch
        {
            AnalysisIssueCode.MissingRequiredDependency => new RepairRecommendation(RepairActionKind.InstallDependency, issue.Severity, packageId, "Install dependency", issue.Explanation, issue.RelatedPackageIds),
            AnalysisIssueCode.InactiveRequiredDependency => new RepairRecommendation(RepairActionKind.ActivateDependency, issue.Severity, packageId, "Enable dependency", issue.Explanation, issue.RelatedPackageIds),
            AnalysisIssueCode.DuplicatePackageId => new RepairRecommendation(RepairActionKind.DisableDuplicate, issue.Severity, packageId, "Resolve duplicate installation", issue.Explanation, issue.RelatedPackageIds),
            AnalysisIssueCode.LoadOrderViolation => new RepairRecommendation(RepairActionKind.ReorderProfile, issue.Severity, packageId, "Apply recommended load order", issue.Explanation, issue.RelatedPackageIds),
            AnalysisIssueCode.DependencyCycle => new RepairRecommendation(RepairActionKind.ReviewCycle, issue.Severity, packageId, "Choose which mod loads first", issue.Explanation, issue.RelatedPackageIds),
            AnalysisIssueCode.LoadOrderBlockedByCycle => new RepairRecommendation(RepairActionKind.ReviewCycle, issue.Severity, packageId, "Resolve blocking dependency cycle", issue.Explanation, issue.RelatedPackageIds),
            _ => new RepairRecommendation(RepairActionKind.InspectMetadata, issue.Severity, packageId, "Inspect mod metadata", issue.Explanation, issue.RelatedPackageIds)
        }).ToArray();
    }

    public ModAnalysisSummary GetSummary(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            return new ModAnalysisSummary(string.Empty, 0, 0, 0, 0, false, "UNKNOWN", "No package ID");

        var issues = GetIssues(packageId);
        var direct = GetDependents(packageId).Count;
        var transitive = GetTransitiveDependents(packageId).Count;
        var errors = issues.Count(issue => issue.Severity == AnalysisIssueSeverity.Error);
        var warnings = issues.Count(issue => issue.Severity == AnalysisIssueSeverity.Warning);
        var health = errors > 0 ? $"{errors} ERROR(S)" : warnings > 0 ? $"{warnings} WARNING(S)" : "HEALTHY";
        var impact = transitive == 0 ? "No dependents" : $"Affects {transitive} mod(s)";
        return new ModAnalysisSummary(
            packageId,
            GetDependencies(packageId).Count,
            direct,
            transitive,
            issues.Count,
            _cycleMembers.Contains(packageId),
            health,
            impact);
    }
}
