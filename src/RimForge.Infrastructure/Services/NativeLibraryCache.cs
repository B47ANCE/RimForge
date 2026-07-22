using System.Text.Json;
using RimForge.Core.Models;

namespace RimForge.Infrastructure.Services;

internal sealed class NativeLibraryCache
{
    private const int SchemaVersion = 2;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    public string Path => _path;

    public NativeLibraryCache(string repositoryRoot, RimForgeConfiguration configuration)
    {
        var outputRoot = System.IO.Path.IsPathRooted(configuration.OutputFolder)
            ? configuration.OutputFolder
            : System.IO.Path.Combine(repositoryRoot, configuration.OutputFolder);
        _path = System.IO.Path.Combine(outputRoot, "Cache", "NativeLibrary.json");
    }

    public async Task<NativeLibraryCacheLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
            return NativeLibraryCacheLoadResult.Empty("Missing");

        try
        {
            await using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                32 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var document = await JsonSerializer.DeserializeAsync<NativeLibraryCacheDocument>(
                stream, JsonOptions, cancellationToken).ConfigureAwait(false);

            if (document is null)
                return NativeLibraryCacheLoadResult.Empty("Unreadable", "The cache document was empty.");
            if (document.SchemaVersion != SchemaVersion)
                return NativeLibraryCacheLoadResult.Empty(
                    "SchemaMismatch",
                    $"Expected schema {SchemaVersion}, found {document.SchemaVersion}.");

            var entries = document.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.RootPath))
                .GroupBy(entry => NormalizePath(entry.RootPath), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
            return new NativeLibraryCacheLoadResult("Loaded", null, entries);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return NativeLibraryCacheLoadResult.Empty("LoadFailed", ex.Message);
        }
    }

    public async Task SaveAsync(IEnumerable<NativeLibraryCacheEntry> entries, CancellationToken cancellationToken)
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var document = new NativeLibraryCacheDocument(
            SchemaVersion,
            DateTimeOffset.Now,
            entries.OrderBy(entry => entry.RootPath, StringComparer.OrdinalIgnoreCase).ToArray());
        var temporaryPath = _path + ".tmp";

        await using (var stream = new FileStream(
            temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
            32 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(temporaryPath, _path, true);
    }

    public static NativeLibrarySignature CreateSignature(string modRoot, string targetVersion)
    {
        var aboutPath = System.IO.Path.Combine(modRoot, "About", "About.xml");
        var aboutInfo = new FileInfo(aboutPath);
        return new NativeLibrarySignature(
            NormalizeVersion(targetVersion),
            aboutInfo.Exists ? aboutInfo.Length : -1,
            aboutInfo.Exists ? aboutInfo.LastWriteTimeUtc.Ticks : 0,
            GetDirectoryTicks(modRoot, "Assemblies"),
            GetDirectoryTicks(modRoot, "Defs"),
            GetDirectoryTicks(modRoot, "Patches"));
    }

    public static string GetMissReason(NativeLibrarySignature cached, NativeLibrarySignature current)
    {
        if (!string.Equals(cached.TargetVersion, current.TargetVersion, StringComparison.OrdinalIgnoreCase))
            return "TargetVersionChanged";
        if (cached.AboutLength != current.AboutLength) return "AboutLengthChanged";
        if (cached.AboutLastWriteTicks != current.AboutLastWriteTicks) return "AboutTimestampChanged";
        if (cached.AssembliesLastWriteTicks != current.AssembliesLastWriteTicks) return "AssembliesChanged";
        if (cached.DefsLastWriteTicks != current.DefsLastWriteTicks) return "DefsChanged";
        if (cached.PatchesLastWriteTicks != current.PatchesLastWriteTicks) return "PatchesChanged";
        return "SignatureMismatch";
    }

    public static string NormalizePath(string path) => System.IO.Path.GetFullPath(path)
        .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

    private static string NormalizeVersion(string version) => version.Trim().ToLowerInvariant();

    private static long GetDirectoryTicks(string root, string child)
    {
        var path = System.IO.Path.Combine(root, child);
        return Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path).Ticks : 0;
    }
}

internal sealed record NativeLibraryCacheLoadResult(
    string Status,
    string? Error,
    IReadOnlyDictionary<string, NativeLibraryCacheEntry> Entries)
{
    public static NativeLibraryCacheLoadResult Empty(string status, string? error = null) => new(
        status, error, new Dictionary<string, NativeLibraryCacheEntry>(StringComparer.OrdinalIgnoreCase));
}

internal sealed record NativeLibraryCacheDocument(
    int SchemaVersion,
    DateTimeOffset Generated,
    IReadOnlyList<NativeLibraryCacheEntry> Entries);

internal sealed record NativeLibraryCacheEntry(
    string RootPath,
    NativeLibrarySignature Signature,
    ModRecord Mod);

internal sealed record NativeLibrarySignature(
    string TargetVersion,
    long AboutLength,
    long AboutLastWriteTicks,
    long AssembliesLastWriteTicks,
    long DefsLastWriteTicks,
    long PatchesLastWriteTicks);
