using RimForge.Analysis.Models;
using RimForge.Core.Models;

namespace RimForge.Analysis.Services;

internal static class AnalysisExplanationBuilder
{
    public static AnalysisExplanationCatalog Build(
        ModAnalysisSnapshot snapshot,
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyList<string>? activeLoadOrder,
        IReadOnlyList<AnalysisDiagnostic> diagnostics)
    {
        var active = (activeLoadOrder ?? Array.Empty<string>())
            .Select(ModNameResolver.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var decisions = snapshot.ProposedOrder.Decisions
            .ToDictionary(item => item.PackageId, StringComparer.OrdinalIgnoreCase);
        var explanations = installedMods
            .Select(mod => BuildMod(snapshot, mod, active, decisions, diagnostics))
            .OrderBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var errors = diagnostics.Count(item => item.Severity == AnalysisIssueSeverity.Error);
        var warnings = diagnostics.Count(item => item.Severity == AnalysisIssueSeverity.Warning);
        var information = diagnostics.Count(item => item.Severity == AnalysisIssueSeverity.Information);
        var affected = explanations.Count(item => item.Diagnostics.Count > 0);
        var status = errors > 0 ? "Blocked" : warnings > 0 ? "Needs attention" : "Healthy";
        var overview = new AnalysisOverview(
            installedMods.Count,
            active.Count,
            installedMods.Count - affected,
            affected,
            errors,
            warnings,
            information,
            snapshot.Relationships.Count(item => item.IsMandatory),
            snapshot.Relationships.Count(item => !item.IsMandatory),
            snapshot.Cycles.Count,
            snapshot.ProposedOrder.IsComplete,
            status,
            BuildOverviewNarrative(installedMods.Count, active.Count, affected, errors, warnings, snapshot));
        return new AnalysisExplanationCatalog(overview, explanations);
    }

    private static ModAnalysisExplanation BuildMod(
        ModAnalysisSnapshot snapshot,
        ModRecord mod,
        IReadOnlySet<string> active,
        IReadOnlyDictionary<string, LoadOrderDecision> decisions,
        IReadOnlyList<AnalysisDiagnostic> diagnostics)
    {
        var packageId = string.IsNullOrWhiteSpace(mod.PackageId)
            ? mod.Id
            : ModNameResolver.Normalize(mod.PackageId);
        var modDiagnostics = diagnostics
            .Where(item => item.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var relationships = snapshot.Relationships
            .Where(item => item.SourcePackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase) ||
                           item.TargetPackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase))
            .Select(item => new AnalysisRelationshipRationale(
                item.SourcePackageId,
                item.TargetPackageId,
                item.Kind,
                item.SourcePackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase) ? "Outgoing" : "Incoming",
                item.Explanation,
                item.RuleSource,
                item.Confidence,
                item.IsMandatory))
            .OrderBy(item => item.Direction, StringComparer.Ordinal)
            .ThenBy(item => item.SourcePackageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.TargetPackageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Kind)
            .ToArray();
        decisions.TryGetValue(packageId, out var decision);
        var summary = snapshot.GetSummary(packageId);
        var recommendations = snapshot.GetRecommendations(packageId);
        return new ModAnalysisExplanation(
            packageId,
            mod.DisplayName,
            active.Contains(packageId),
            summary,
            modDiagnostics,
            relationships,
            recommendations,
            decision,
            BuildModNarrative(mod.DisplayName, summary, modDiagnostics, relationships, decision));
    }

    private static string BuildOverviewNarrative(
        int installed,
        int active,
        int affected,
        int errors,
        int warnings,
        ModAnalysisSnapshot snapshot) =>
        $"Analyzed {installed} installed mod(s), including {active} active mod(s). " +
        $"{affected} mod(s) have findings: {errors} error(s) and {warnings} warning(s). " +
        (snapshot.ProposedOrder.IsComplete
            ? "A complete deterministic load order is available."
            : $"A complete load order is blocked for {snapshot.ProposedOrder.BlockedPackageIds.Count} mod(s).");

    private static string BuildModNarrative(
        string displayName,
        ModAnalysisSummary summary,
        IReadOnlyList<AnalysisDiagnostic> diagnostics,
        IReadOnlyList<AnalysisRelationshipRationale> relationships,
        LoadOrderDecision? decision)
    {
        var health = diagnostics.Count == 0
            ? "has no detected findings"
            : $"has {diagnostics.Count} finding(s), including {diagnostics.Count(item => item.Severity == AnalysisIssueSeverity.Error)} error(s)";
        var placement = decision is null
            ? "No load-order placement decision applies."
            : $"Proposed position {decision.ProposedIndex + 1}: {decision.PrimaryReason}.";
        return $"{displayName} {health}, {summary.RequiredDependencyCount} required dependency(ies), " +
               $"and {summary.TransitiveDependentCount} transitive dependent(s). " +
               $"{relationships.Count} relationship rationale record(s) apply. {placement}";
    }
}
