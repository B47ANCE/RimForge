namespace RimForge.Analysis.Models;

public sealed record AnalysisOverview(
    int InstalledModCount,
    int ActiveModCount,
    int HealthyModCount,
    int AffectedModCount,
    int ErrorCount,
    int WarningCount,
    int InformationCount,
    int MandatoryRelationshipCount,
    int AdvisoryRelationshipCount,
    int CycleCount,
    bool HasCompleteLoadOrder,
    string Status,
    string Narrative);

public sealed record AnalysisRelationshipRationale(
    string SourcePackageId,
    string TargetPackageId,
    AnalysisRelationshipKind Kind,
    string Direction,
    string Explanation,
    string RuleSource,
    RimForge.Core.Models.LoadOrderRuleConfidence Confidence,
    bool IsMandatory);

public sealed record ModAnalysisExplanation(
    string PackageId,
    string DisplayName,
    bool IsActive,
    ModAnalysisSummary Summary,
    IReadOnlyList<AnalysisDiagnostic> Diagnostics,
    IReadOnlyList<AnalysisRelationshipRationale> Relationships,
    IReadOnlyList<RepairRecommendation> Recommendations,
    LoadOrderDecision? LoadOrderDecision,
    string Narrative);

public sealed class AnalysisExplanationCatalog
{
    private readonly IReadOnlyDictionary<string, ModAnalysisExplanation> _byPackageId;

    public AnalysisExplanationCatalog(
        AnalysisOverview overview,
        IReadOnlyList<ModAnalysisExplanation> mods)
    {
        Overview = overview;
        Mods = mods;
        _byPackageId = mods
            .GroupBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public AnalysisOverview Overview { get; }
    public IReadOnlyList<ModAnalysisExplanation> Mods { get; }

    public ModAnalysisExplanation? GetMod(string? packageId) =>
        !string.IsNullOrWhiteSpace(packageId) && _byPackageId.TryGetValue(packageId, out var explanation)
            ? explanation
            : null;
}
