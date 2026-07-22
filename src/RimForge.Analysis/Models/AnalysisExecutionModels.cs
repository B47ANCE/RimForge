using RimForge.Core.Models;

namespace RimForge.Analysis.Models;

public sealed record ModAnalysisRequest(
    IReadOnlyList<ModRecord> InstalledMods,
    IReadOnlyList<string>? ActiveLoadOrder = null,
    string? TargetRimWorldVersion = null,
    IReadOnlyList<UserLoadOrderLock>? LockedPositions = null,
    AnalysisCachePolicy CachePolicy = AnalysisCachePolicy.Use,
    IReadOnlyList<ForgeEvidenceContribution>? Evidence = null);

public enum AnalysisCachePolicy
{
    Use,
    Refresh,
    Bypass
}

public enum AnalysisCacheDisposition
{
    Miss,
    Hit,
    Refreshed,
    Bypassed
}

public sealed record AnalysisCacheInfo(
    AnalysisCacheDisposition Disposition,
    string Key,
    DateTimeOffset GeneratedAtUtc);

public sealed record AnalysisExecutionMetrics(
    int InstalledLibraryCount,
    int ActiveProfileCount,
    int RelationshipCount,
    int IssueCount,
    int CycleCount,
    TimeSpan Elapsed,
    string InputFingerprint);

public enum AnalysisStage
{
    CacheLookup,
    Indexing,
    Relationships,
    Rules,
    GraphValidation,
    ProfileValidation,
    LoadOrderPlanning,
    Finalizing,
    Complete
}

public sealed record AnalysisStageMetrics(
    AnalysisStage Stage,
    TimeSpan Elapsed);

public sealed record AnalysisDiagnostic(
    string Code,
    AnalysisIssueSeverity Severity,
    string Message,
    string PackageId,
    IReadOnlyList<string> RelatedPackageIds);

public sealed record ModAnalysisResult(
    ModAnalysisSnapshot Snapshot,
    AnalysisExecutionMetrics Metrics,
    IReadOnlyList<AnalysisDiagnostic> Diagnostics,
    IReadOnlyList<AnalysisStageMetrics> Stages,
    AnalysisCacheInfo Cache,
    AnalysisExplanationCatalog Explainability);

public sealed record AnalysisProgress(AnalysisStage Stage, int Completed, int Total, string Message);
