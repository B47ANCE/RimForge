namespace RimForge.Core.Models;

public sealed record ExternalProfileSnapshot(
    string Path,
    string Version,
    IReadOnlyList<string> ActiveMods,
    IReadOnlyList<string> KnownExpansions,
    DateTimeOffset ReadUtc);

public sealed record ExternalProfileReconciliation(
    RimForgeProfile Profile,
    ExternalProfileSnapshot External,
    IReadOnlyList<string> AddedPackageIds,
    IReadOnlyList<string> RemovedPackageIds,
    IReadOnlyList<ProfileOrderChange> OrderChanges)
{
    public bool IsIdentical => AddedPackageIds.Count == 0 && RemovedPackageIds.Count == 0 && OrderChanges.Count == 0;
    public string Summary => IsIdentical
        ? "The active RimWorld configuration matches the selected RimForge profile."
        : $"{AddedPackageIds.Count} added, {RemovedPackageIds.Count} removed, {OrderChanges.Count} reordered.";
}
