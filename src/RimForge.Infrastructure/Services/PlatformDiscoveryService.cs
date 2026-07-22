using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class WorkspaceService : IWorkspaceService
{
    public WorkspaceService(RimForgePathLayout paths) => Paths = paths ?? throw new ArgumentNullException(nameof(paths));
    public RimForgePathLayout Paths { get; }
    public void EnsureCreated() => Paths.EnsureGeneratedDirectories();
}

public sealed class PlatformDiscoveryService : IPlatformDiscoveryService, IRimWorldInstallationService
{
    private readonly ISteamLibraryService _steamLibraries;
    private readonly IWorkspaceService _workspace;
    private readonly string? _localApplicationData;

    public PlatformDiscoveryService(
        ISteamLibraryService steamLibraries,
        IWorkspaceService workspace,
        string? localApplicationData = null)
    {
        _steamLibraries = steamLibraries ?? throw new ArgumentNullException(nameof(steamLibraries));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _localApplicationData = localApplicationData;
    }

    public IReadOnlyList<SteamInstallationCandidate> FindInstallations(
        IEnumerable<string>? additionalSteamRoots = null) =>
        _steamLibraries.FindRimWorldInstallations(additionalSteamRoots);

    public PlatformDiscoverySnapshot Discover(IEnumerable<string>? additionalSteamRoots = null)
    {
        var installations = FindInstallations(additionalSteamRoots);
        var preferred = installations
            .OrderByDescending(candidate => candidate.CanLaunchDirectly)
            .ThenByDescending(candidate => Directory.Exists(candidate.WorkshopFolder))
            .ThenBy(candidate => candidate.LibraryRoot, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return new PlatformDiscoverySnapshot(
            DateTimeOffset.UtcNow,
            installations,
            preferred,
            ResolveUserPaths(),
            _workspace.Paths);
    }

    private RimWorldUserPaths ResolveUserPaths()
    {
        var local = _localApplicationData;
        if (string.IsNullOrWhiteSpace(local))
            local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            local = _workspace.Paths.LocalApplicationDataRoot;

        var localFullPath = Path.GetFullPath(local);
        var parent = Directory.GetParent(localFullPath)?.FullName;
        var localLow = Path.GetFileName(localFullPath).Equals("LocalLow", StringComparison.OrdinalIgnoreCase)
            ? localFullPath
            : Path.Combine(parent ?? localFullPath, "LocalLow");
        var userData = Path.Combine(localLow, "Ludeon Studios", "RimWorld by Ludeon Studios");
        var config = Path.Combine(userData, "Config");
        return new RimWorldUserPaths(
            userData,
            config,
            Path.Combine(config, "ModsConfig.xml"),
            Path.Combine(userData, "Player.log"));
    }
}
