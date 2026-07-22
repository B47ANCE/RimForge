using System.Text.RegularExpressions;
using Microsoft.Win32;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed partial class SteamLibraryDiscoveryService : ISteamLibraryDiscoveryService
{
    private const string RimWorldAppId = "294100";

    public IReadOnlyList<SteamInstallationCandidate> FindRimWorldInstallations(
        IEnumerable<string>? additionalRoots = null)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in additionalRoots ?? Array.Empty<string>()) AddRoot(roots, root);
        AddRegistryRoots(roots);
        AddRoot(roots, Environment.GetEnvironmentVariable("ProgramFiles(x86)") is { Length: > 0 } x86
            ? Path.Combine(x86, "Steam") : null);
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) is { Length: > 0 } pf
            ? Path.Combine(pf, "Steam") : null);

        foreach (var steamRoot in roots.ToArray())
        {
            foreach (var library in ReadLibraryFolders(steamRoot)) AddRoot(roots, library);
        }

        return roots
            .Select(CreateCandidate)
            .Where(candidate => candidate is not null)
            .Cast<SteamInstallationCandidate>()
            .DistinctBy(candidate => candidate.LibraryRoot, StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate.LibraryRoot, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SteamInstallationCandidate? CreateCandidate(string libraryRoot)
    {
        var steamApps = Path.Combine(libraryRoot, "steamapps");
        var workshop = Path.Combine(steamApps, "workshop", "content", RimWorldAppId);
        var manifest = Path.Combine(steamApps, $"appmanifest_{RimWorldAppId}.acf");
        var installDirectoryName = ReadInstallDirectoryName(manifest) ?? "RimWorld";
        var gameRoot = Path.Combine(steamApps, "common", installDirectoryName);
        var localMods = Path.Combine(gameRoot, "Mods");

        if (!Directory.Exists(workshop) && !Directory.Exists(localMods) && !File.Exists(manifest)) return null;
        var gameExecutable = Path.Combine(gameRoot, "RimWorldWin64.exe");
        var steamExecutable = FindSteamExecutable(libraryRoot);
        return new SteamInstallationCandidate(
            libraryRoot,
            workshop,
            localMods,
            File.Exists(gameExecutable) ? gameExecutable : null,
            steamExecutable);
    }


    private static string? ReadInstallDirectoryName(string manifestPath)
    {
        if (!File.Exists(manifestPath)) return null;
        try
        {
            var text = File.ReadAllText(manifestPath);
            var match = Regex.Match(text, @"""installdir""\s+""([^""]+)""", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindSteamExecutable(string libraryRoot)
    {
        foreach (var candidate in new[]
        {
            Path.Combine(libraryRoot, "steam.exe"),
            Path.Combine(Directory.GetParent(libraryRoot)?.FullName ?? libraryRoot, "steam.exe")
        })
        {
            if (File.Exists(candidate)) return candidate;
        }

        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
            var steamExe = key?.GetValue("SteamExe") as string;
            if (!string.IsNullOrWhiteSpace(steamExe) && File.Exists(steamExe)) return steamExe;
        }
        catch { }
        return null;
    }

    private static IEnumerable<string> ReadLibraryFolders(string root)
    {
        var path = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(path)) yield break;

        string text;
        try { text = File.ReadAllText(path); }
        catch { yield break; }

        foreach (Match match in LibraryPathRegex().Matches(text))
        {
            var value = match.Groups[1].Value.Replace("\\\\", "\\");
            if (!string.IsNullOrWhiteSpace(value)) yield return value;
        }
    }

    private static void AddRegistryRoots(ISet<string> roots)
    {
        if (!OperatingSystem.IsWindows()) return;

        foreach (var keyPath in new[] { @"SOFTWARE\Valve\Steam", @"SOFTWARE\WOW6432Node\Valve\Steam" })
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                AddRoot(roots, key?.GetValue("InstallPath") as string);
            }
            catch { }
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
            AddRoot(roots, key?.GetValue("SteamPath") as string);
        }
        catch { }
    }

    private static void AddRoot(ISet<string> roots, string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return;
        try
        {
            var normalized = Path.GetFullPath(root.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar);
            if (Directory.Exists(normalized)) roots.Add(normalized);
        }
        catch { }
    }

    [GeneratedRegex("\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();
}
