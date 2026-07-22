using System.Security.Cryptography;
using System.Text.Json;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class StatePreservationService : IStatePreservationService
{
    private readonly RimForgePathLayout _paths;
    private readonly string[] _protectedRoots;

    public StatePreservationService(RimForgePathLayout paths)
    {
        _paths = paths;
        _protectedRoots = new[] { paths.OutputRoot, paths.ProfilesRoot, paths.CacheRoot, paths.SessionsRoot, paths.DiagnosticsRoot }
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void ValidateInstallBoundary(string installRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        var install = Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var protectedRoot in _protectedRoots)
        {
            var protectedPath = protectedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (install.StartsWith(protectedPath, StringComparison.OrdinalIgnoreCase) ||
                protectedPath.StartsWith(install, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Install root overlaps protected application state: {protectedRoot}");
        }
    }

    public async Task<PreservedStateManifest> CaptureAsync(string applicationVersion, CancellationToken cancellationToken = default)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in new[] { "Config.json", "Features.json" })
        {
            var path = Path.Combine(_paths.RepositoryRoot, file);
            if (!File.Exists(path)) continue;
            await using var stream = File.OpenRead(path);
            hashes[file] = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        }

        var manifest = new PreservedStateManifest(applicationVersion, DateTimeOffset.UtcNow, _protectedRoots, hashes);
        var manifestPath = Path.Combine(_paths.LocalApplicationDataRoot, "Recovery", "preserved-state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var temporary = manifestPath + ".tmp";
        await using (var output = File.Create(temporary))
            await JsonSerializer.SerializeAsync(output, manifest, cancellationToken: cancellationToken).ConfigureAwait(false);
        File.Move(temporary, manifestPath, true);
        return manifest;
    }
}
