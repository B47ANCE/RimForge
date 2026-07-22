namespace RimForge.Core.Models;

public sealed record ModValidationSummary(
    int MissingNames,
    int MissingPackageIds,
    int DuplicatePackageIds,
    int DuplicateWorkshopIds,
    IReadOnlyList<string> DuplicatePackageIdValues,
    IReadOnlyList<string> DuplicateWorkshopIdValues);

public sealed record MissingDependency(
    string SourcePackageId,
    string SourceName,
    string RequiredPackageId,
    string? RequiredDisplayName);

public sealed record DependencyCycle(IReadOnlyList<string> PackageIds)
{
    public string DisplayText => string.Join(" → ", PackageIds);
}

public sealed record ModLibrarySnapshot(
    IReadOnlyList<ModRecord> Mods,
    ModValidationSummary Validation,
    DependencyGraphModel DependencyGraph,
    IReadOnlyList<MissingDependency> MissingDependencies,
    IReadOnlyList<DependencyCycle> Cycles,
    DateTimeOffset Generated)
{
    public NativeLibraryCacheMetrics? CacheMetrics { get; init; }

    public static ModLibrarySnapshot Empty { get; } = new(
        Array.Empty<ModRecord>(),
        new ModValidationSummary(0, 0, 0, 0, Array.Empty<string>(), Array.Empty<string>()),
        new DependencyGraphModel(Array.Empty<DependencyGraphNode>(), Array.Empty<DependencyGraphEdge>()),
        Array.Empty<MissingDependency>(),
        Array.Empty<DependencyCycle>(),
        DateTimeOffset.MinValue);
}

public sealed record NativeScanProgress(string Stage, int Completed, int Total, string Message)
{
    public int Percentage => Total <= 0 ? 0 : (int)Math.Round(Completed * 100d / Total);
}
