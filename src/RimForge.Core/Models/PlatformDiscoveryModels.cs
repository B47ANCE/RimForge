namespace RimForge.Core.Models;

public sealed record RimWorldUserPaths(
    string UserDataRoot,
    string ConfigRoot,
    string ModsConfigPath,
    string PlayerLogPath);

public sealed record PlatformDiscoverySnapshot(
    DateTimeOffset DiscoveredUtc,
    IReadOnlyList<SteamInstallationCandidate> Installations,
    SteamInstallationCandidate? PreferredInstallation,
    RimWorldUserPaths UserPaths,
    RimForgePathLayout Workspace)
{
    public bool IsRimWorldInstalled => Installations.Count > 0;
    public IReadOnlyList<string> WorkshopRoots => Installations
        .Select(installation => installation.WorkshopFolder)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
