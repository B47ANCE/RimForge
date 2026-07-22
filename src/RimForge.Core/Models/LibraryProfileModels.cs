namespace RimForge.Core.Models;

public enum ProfileModResolution
{
    Installed,
    Missing,
    Ambiguous
}

public sealed record ProfileModReference(
    string PackageId,
    int Position,
    ProfileModResolution Resolution,
    ModRecord? Mod = null);

public enum ProfileReadinessStatus
{
    Ready,
    Warning,
    Blocked
}

public sealed record ProfileReadinessSummary(
    ProfileReadinessStatus Status,
    int MissingCount,
    int AmbiguousCount,
    int DuplicateActiveCount,
    int IncompatibleCount,
    bool HasCore,
    IReadOnlyList<string> Reasons)
{
    public bool CanActivate => Status != ProfileReadinessStatus.Blocked;
}

public sealed record ProfileLibraryProjection(
    RimForgeProfile Profile,
    IReadOnlyList<ProfileModReference> ActiveMods,
    IReadOnlyList<ModRecord> InactiveInstalledMods,
    ProfileReadinessSummary Readiness)
{
    public int InstalledCount => ActiveMods.Count(item => item.Resolution == ProfileModResolution.Installed);
    public int MissingCount => ActiveMods.Count(item => item.Resolution == ProfileModResolution.Missing);
    public int AmbiguousCount => ActiveMods.Count(item => item.Resolution == ProfileModResolution.Ambiguous);
    public bool IsResolved => MissingCount == 0 && AmbiguousCount == 0;
}

public sealed record LibraryProfileWorkspaceSnapshot(
    IReadOnlyList<ModRecord> InstalledMods,
    IReadOnlyList<ProfileLibraryProjection> Profiles,
    IReadOnlyList<string> DuplicatePackageIds,
    string Fingerprint,
    DateTimeOffset GeneratedUtc)
{
    public ProfileLibraryProjection? FindProfile(string name) => Profiles.FirstOrDefault(
        profile => string.Equals(profile.Profile.Name, name, StringComparison.OrdinalIgnoreCase));
}
