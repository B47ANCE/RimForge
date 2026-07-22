namespace RimForge.Analysis.Models;

public enum IssueScopeKind
{
    ActiveProfile,
    FullLibrary
}

public enum IssueResolutionKind
{
    Automatic,
    Assisted,
    Manual
}

public sealed record IssueRelatedMod(string PackageId, string DisplayName);

public sealed record IssueWorkItem(
    string Id,
    string PackageId,
    string ModName,
    AnalysisIssueCode Code,
    AnalysisIssueSeverity Severity,
    string Category,
    string Title,
    string Explanation,
    string WhyItMatters,
    string RecommendedAction,
    RepairActionKind RepairAction,
    IssueResolutionKind ResolutionKind,
    bool CanAutoFix,
    bool IsIgnored,
    IReadOnlyList<string> RelatedPackageIds,
    IReadOnlyList<string> RelatedModNames)
{
    public string SeverityLabel => IsIgnored ? $"IGNORED {Severity.ToString().ToUpperInvariant()}" : Severity.ToString().ToUpperInvariant();
    public string IgnoreActionLabel => IsIgnored ? "Unignore" : "Ignore";
    public double DisplayOpacity => IsIgnored ? 0.42 : 1.0;
    public string ResolutionLabel => ResolutionKind switch
    {
        IssueResolutionKind.Automatic => "Automatic repair",
        IssueResolutionKind.Assisted => "Assisted repair",
        _ => "User decision required"
    };

    public string RelatedModsText => RelatedModNames.Count == 0
        ? "None"
        : string.Join(" • ", RelatedModNames);

    public IReadOnlyList<IssueRelatedMod> RelatedMods => RelatedPackageIds
        .Select((packageId, index) => new IssueRelatedMod(
            packageId,
            index < RelatedModNames.Count && !string.IsNullOrWhiteSpace(RelatedModNames[index])
                ? RelatedModNames[index]
                : packageId))
        .ToArray();
}

public sealed record IssueScopeSummary(
    IssueScopeKind Scope,
    string ScopeName,
    int ErrorCount,
    int WarningCount,
    int InformationCount,
    int ModCount,
    int IssueCount)
{
    public string CanonicalStatus => ErrorCount == 0 && WarningCount == 0
        ? "Healthy"
        : ErrorCount > 0 && WarningCount > 0
            ? $"{ErrorCount} Errors • {WarningCount} Warnings"
            : ErrorCount > 0
                ? $"{ErrorCount} Errors"
                : $"{WarningCount} Warnings";
}

public sealed record IssueViewerSnapshot(
    IssueScopeSummary Summary,
    IReadOnlyList<IssueWorkItem> Issues)
{
    public IReadOnlyList<IssueWorkItem> ActiveIssues => Issues.Where(issue => !issue.IsIgnored).ToArray();
}
