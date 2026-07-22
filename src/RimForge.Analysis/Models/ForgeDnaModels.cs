using RimForge.Core.Models;

namespace RimForge.Analysis.Models;

public enum ForgeDnaHealthState
{
    Unknown,
    Healthy,
    Information,
    Warning,
    Error
}

public sealed record ForgeDnaFingerprint(
    string Value,
    DateTimeOffset GeneratedAt,
    DateTimeOffset SourceLastModified,
    int EvidenceFileCount,
    int DependencyCount);

public sealed record ForgeDnaTechnology(
    ModEvidenceKind Kind,
    string Label,
    int Count,
    string Summary,
    IReadOnlyList<string> EvidenceFiles);

public sealed record ForgeDnaRecord(
    string StableId,
    string PackageId,
    string DisplayName,
    string? Author,
    ModSource Source,
    string RootPath,
    IReadOnlyList<string> SupportedVersions,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Dependents,
    IReadOnlyList<ForgeDnaTechnology> Technologies,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Findings,
    IReadOnlyList<ModAnalysisIssue> Issues,
    ForgeDnaHealthState Health,
    ForgeDnaFingerprint Fingerprint)
{
    public int IssueCount => Issues.Count;
    public int ErrorCount => Issues.Count(issue => issue.Severity == AnalysisIssueSeverity.Error);
    public int WarningCount => Issues.Count(issue => issue.Severity == AnalysisIssueSeverity.Warning);
    public string TechnologySummary => Technologies.Count == 0
        ? "No technology evidence discovered"
        : string.Join(" · ", Technologies.Select(technology => technology.Label));
}

public sealed record ForgeDnaMetrics(
    int TotalMods,
    int ReusedRecords,
    int RebuiltRecords,
    TimeSpan Elapsed,
    DateTimeOffset GeneratedAt);

public sealed class ForgeDnaSnapshot
{
    private readonly IReadOnlyDictionary<string, ForgeDnaRecord> _byPackageId;

    public ForgeDnaSnapshot(
        IReadOnlyList<ForgeDnaRecord> records,
        ModAnalysisSnapshot analysis,
        ForgeDnaMetrics metrics)
    {
        Records = records;
        Analysis = analysis;
        Metrics = metrics;
        _byPackageId = records
            .Where(record => !string.IsNullOrWhiteSpace(record.PackageId))
            .GroupBy(record => record.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ForgeDnaRecord> Records { get; }
    public ModAnalysisSnapshot Analysis { get; }
    public ForgeDnaMetrics Metrics { get; }

    public ForgeDnaRecord? Find(string? packageId) =>
        packageId is not null && _byPackageId.TryGetValue(packageId, out var record) ? record : null;
}
