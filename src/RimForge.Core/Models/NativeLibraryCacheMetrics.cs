namespace RimForge.Core.Models;

public sealed record NativeLibraryCacheMissReason(string Reason, int Count);

public sealed record NativeLibraryCacheMetrics(
    string CachePath,
    string LoadStatus,
    string? LoadError,
    int DiscoveredFolders,
    int CachedEntries,
    int CacheHits,
    int CacheMisses,
    int AddedEntries,
    int RemovedEntries,
    int ReparsedEntries,
    double LoadMilliseconds,
    double SignatureMilliseconds,
    double ParseMilliseconds,
    double SaveMilliseconds,
    IReadOnlyList<NativeLibraryCacheMissReason> MissReasons)
{
    public double DiscoveryMilliseconds { get; init; }
    public double MaterializationMilliseconds { get; init; }
    public double ValidationMilliseconds { get; init; }
    public double DependencyGraphMilliseconds { get; init; }
    public double ServiceTotalMilliseconds { get; init; }
}

public sealed record StartupUiProjectionMetrics(
    int PreliminaryRecordsPublished,
    double PreliminaryProjectionMilliseconds,
    double AnalysisMilliseconds,
    double SorterProjectionBuildMilliseconds,
    double ModsCollectionMilliseconds,
    double DependencyEdgesCollectionMilliseconds,
    double ModSorterCollectionMilliseconds,
    double ProfileLoadMilliseconds,
    double TotalApplySnapshotMilliseconds)
{
    public double TimeToUsableMilliseconds { get; init; }
    public double FirstRenderToUsableMilliseconds { get; init; }
}
