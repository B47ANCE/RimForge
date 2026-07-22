namespace RimForge.Core.Models;

public sealed record MissingDependencyRequirement(
    string PackageId,
    string DisplayName,
    IReadOnlyList<string> RequiredByPackageIds,
    IReadOnlyList<string> Path);

public sealed record DependencyActivationPlan(
    IReadOnlyList<string> RequestedPackageIds,
    IReadOnlyList<string> DependencyPackageIds,
    IReadOnlyList<MissingDependencyRequirement> MissingDependencies,
    IReadOnlyList<IReadOnlyList<string>> Cycles)
{
    public bool HasInstalledDependencies => DependencyPackageIds.Count > 0;
    public bool HasMissingDependencies => MissingDependencies.Count > 0;
}

public sealed record DependencyRemovalPlan(
    IReadOnlyList<string> RequestedPackageIds,
    IReadOnlyList<DependencyReason> ImpactedDependents,
    IReadOnlyList<string> CascadePackageIds,
    IReadOnlyList<DependencyReason> OrphanCandidates)
{
    public bool IsSafe => ImpactedDependents.Count == 0;
}

public sealed record DependencyManagementSummary(
    int ActiveModCount,
    int MissingDependencyCount,
    int OrphanCandidateCount,
    int DependencyBearingModCount,
    bool IsHealthy,
    string Summary);

public enum OrphanCleanupMode
{
    Automatic,
    Ask,
    Manual
}
