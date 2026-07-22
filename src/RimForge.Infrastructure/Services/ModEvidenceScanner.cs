using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using RimForge.Core.Models;

namespace RimForge.Infrastructure.Services;

internal enum ModEvidenceCacheStatus
{
    Hit,
    Miss,
    FingerprintMismatch,
    SchemaMismatch,
    Corrupt
}

internal sealed record ModEvidenceScanResult(
    ModEvidence Evidence,
    bool CacheHit,
    string Fingerprint,
    ModEvidenceCacheStatus CacheStatus,
    TimeSpan Elapsed);

internal sealed record ModEvidenceCacheCleanupResult(
    int CacheFilesDeleted,
    int TemporaryFilesDeleted,
    int QuarantineFilesDeleted);

internal static class ModEvidenceScanner
{
    private const int CacheSchemaVersion = 4;
    private static readonly JsonSerializerOptions CacheJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly HashSet<string> TextureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".dds", ".jpg", ".jpeg", ".tga", ".psd"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ogg", ".wav", ".mp3"
    };

    private static readonly HashSet<string> PrunedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".hg", ".vs", "bin", "obj", "__pycache__", "node_modules",
        "Screenshots", "Screenshot", "Backups", "Backup", "Docs", "Documentation"
    };

    public static async Task<ModEvidenceScanResult> ScanOrLoadAsync(
        string rootPath,
        string repositoryRoot,
        string targetRimWorldVersion,
        CancellationToken cancellationToken,
        bool forceRescan = false)
    {
        var started = DateTime.UtcNow;
        var cacheDirectory = Path.Combine(RimForgePathLayout.Create(repositoryRoot).CacheRoot, "ForgeEvidence");
        Directory.CreateDirectory(cacheDirectory);

        var normalizedTargetVersion = NormalizeTargetVersion(targetRimWorldVersion);
        var fingerprint = BuildQuickFingerprint(rootPath, normalizedTargetVersion);
        var cachePath = Path.Combine(cacheDirectory, $"{HashPath(rootPath)}-{normalizedTargetVersion.Replace('.', '_')}.json");
        var cacheRead = forceRescan
            ? new EvidenceCacheReadResult(null, ModEvidenceCacheStatus.Miss)
            : await TryReadCacheAsync(cachePath, fingerprint, cancellationToken).ConfigureAwait(false);
        if (cacheRead.Evidence is not null)
        {
            return new ModEvidenceScanResult(
                cacheRead.Evidence,
                true,
                fingerprint,
                ModEvidenceCacheStatus.Hit,
                DateTime.UtcNow - started);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var evidence = ScanCore(rootPath, normalizedTargetVersion, cancellationToken);
        await WriteCacheAsync(cachePath, rootPath, normalizedTargetVersion, fingerprint, evidence, cancellationToken).ConfigureAwait(false);
        return new ModEvidenceScanResult(
            evidence,
            false,
            fingerprint,
            cacheRead.Status,
            DateTime.UtcNow - started);
    }

    public static Task<ModEvidenceCacheCleanupResult> CleanupCacheAsync(
        string repositoryRoot,
        IEnumerable<string> activeRootPaths,
        string targetRimWorldVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cacheDirectory = Path.Combine(RimForgePathLayout.Create(repositoryRoot).CacheRoot, "ForgeEvidence");
        if (!Directory.Exists(cacheDirectory))
            return Task.FromResult(new ModEvidenceCacheCleanupResult(0, 0, 0));

        var normalizedTargetVersion = NormalizeTargetVersion(targetRimWorldVersion);
        var versionSuffix = $"-{normalizedTargetVersion.Replace('.', '_')}.json";
        var activeCacheNames = activeRootPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => $"{HashPath(path)}{versionSuffix}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var cacheFilesDeleted = 0;
        var temporaryFilesDeleted = 0;
        var quarantineFilesDeleted = 0;

        foreach (var path in Directory.EnumerateFiles(cacheDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(path);
            try
            {
                if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                {
                    if (now - File.GetLastWriteTimeUtc(path) >= TimeSpan.FromHours(24))
                    {
                        File.Delete(path);
                        temporaryFilesDeleted++;
                    }
                    continue;
                }

                if (name.Contains(".corrupt-", StringComparison.OrdinalIgnoreCase))
                {
                    if (now - File.GetLastWriteTimeUtc(path) >= TimeSpan.FromDays(7))
                    {
                        File.Delete(path);
                        quarantineFilesDeleted++;
                    }
                    continue;
                }

                if (name.EndsWith(versionSuffix, StringComparison.OrdinalIgnoreCase) &&
                    !activeCacheNames.Contains(name))
                {
                    File.Delete(path);
                    cacheFilesDeleted++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Cache maintenance is best-effort and must never block publication.
            }
        }

        return Task.FromResult(new ModEvidenceCacheCleanupResult(
            cacheFilesDeleted,
            temporaryFilesDeleted,
            quarantineFilesDeleted));
    }

    private static ModEvidence ScanCore(string rootPath, string targetRimWorldVersion, CancellationToken cancellationToken)
    {
        try
        {
            var xmlFiles = new List<string>();
            var assemblyFiles = new List<string>();
            var textureFiles = new List<string>();
            var audioFiles = new List<string>();
            var languageFiles = new List<string>();
            var totalFiles = 0;
            long totalBytes = 0;
            var harmonyHints = 0;

            foreach (var file in EnumerateFilesPruned(rootPath, targetRimWorldVersion, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalFiles++;
                totalBytes += SafeLength(file);

                var extension = Path.GetExtension(file);
                if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)) xmlFiles.Add(file);
                else if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    assemblyFiles.Add(file);
                    if (Path.GetFileName(file).Contains("harmony", StringComparison.OrdinalIgnoreCase)) harmonyHints++;
                }
                else if (TextureExtensions.Contains(extension)) textureFiles.Add(file);
                else if (AudioExtensions.Contains(extension)) audioFiles.Add(file);

                if (IsUnderFolder(file, "Languages")) languageFiles.Add(file);
            }

            var counters = new Dictionary<ModEvidenceKind, int>();
            var definitionCount = 0;
            var patchCount = 0;
            var findings = new List<string>();

            foreach (var xmlPath in xmlFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var stream = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 32 * 1024, FileOptions.SequentialScan);
                    using var reader = XmlReader.Create(stream, new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Ignore,
                        IgnoreComments = true,
                        IgnoreWhitespace = true,
                        CloseInput = false
                    });

                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element) continue;
                        var name = reader.LocalName;
                        if (name.EndsWith("Def", StringComparison.OrdinalIgnoreCase))
                        {
                            definitionCount++;
                            IncrementCapabilityCounter(counters, name);
                        }
                        if (name.Contains("PatchOperation", StringComparison.OrdinalIgnoreCase)) patchCount++;
                    }
                }
                catch
                {
                    findings.Add($"Could not parse {Path.GetFileName(xmlPath)}");
                }
            }

            if (patchCount > 0) counters[ModEvidenceKind.PatchOperations] = patchCount;
            if (definitionCount > 0) counters[ModEvidenceKind.Definitions] = definitionCount;
            if (assemblyFiles.Count > 0)
            {
                counters[ModEvidenceKind.CSharp] = assemblyFiles.Count;
                counters[ModEvidenceKind.Assemblies] = assemblyFiles.Count;
            }
            if (harmonyHints > 0) counters[ModEvidenceKind.Harmony] = harmonyHints;
            if (xmlFiles.Count > 0) counters[ModEvidenceKind.Xml] = xmlFiles.Count;
            if (textureFiles.Count > 0) counters[ModEvidenceKind.Textures] = textureFiles.Count;
            if (audioFiles.Count > 0) counters[ModEvidenceKind.Audio] = audioFiles.Count;
            if (languageFiles.Count > 0) counters[ModEvidenceKind.Languages] = languageFiles.Count;

            var badges = counters
                .Select(pair => CreateBadge(pair.Key, pair.Value, rootPath, xmlFiles, assemblyFiles, textureFiles, audioFiles, languageFiles, definitionCount, patchCount))
                .OrderBy(badge => BadgePriority(badge.Kind))
                .ThenBy(badge => badge.Label, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var capabilities = BuildCapabilities(counters).ToArray();
            if (assemblyFiles.Count > 0) findings.Add($"{assemblyFiles.Count} managed assembly file(s) discovered.");
            if (patchCount > 0) findings.Add($"{patchCount} XML patch operation(s) discovered.");
            if (textureFiles.Count > 0) findings.Add($"{textureFiles.Count} texture asset(s) discovered.");

            return new ModEvidence
            {
                TotalFiles = totalFiles,
                TotalBytes = totalBytes,
                XmlFiles = xmlFiles.Count,
                AssemblyFiles = assemblyFiles.Count,
                TextureFiles = textureFiles.Count,
                AudioFiles = audioFiles.Count,
                LanguageFiles = languageFiles.Count,
                DefinitionCount = definitionCount,
                PatchOperationCount = patchCount,
                HarmonyHintCount = harmonyHints,
                Badges = badges,
                Capabilities = capabilities,
                NotableFindings = findings.Take(8).ToArray()
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ModEvidence { NotableFindings = [$"Evidence scan failed: {ex.Message}"] };
        }
    }

    private static IEnumerable<string> EnumerateFilesPruned(string rootPath, string targetRimWorldVersion, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(directory); }
            catch { continue; }
            foreach (var file in files) yield return file;

            IEnumerable<string> children;
            try { children = Directory.EnumerateDirectories(directory); }
            catch { continue; }
            foreach (var child in children)
            {
                var directoryName = Path.GetFileName(child);
                if (PrunedDirectoryNames.Contains(directoryName)) continue;
                if (TryGetVersionFolder(directoryName, out var folderVersion) &&
                    !string.Equals(folderVersion, targetRimWorldVersion, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                pending.Push(child);
            }
        }
    }

    private static string BuildQuickFingerprint(string rootPath, string targetRimWorldVersion)
    {
        var builder = new StringBuilder(CacheSchemaVersion.ToString()).Append("|").Append(targetRimWorldVersion);
        AppendPathStamp(builder, Path.Combine(rootPath, "About", "About.xml"));
        AppendDirectoryStamp(builder, rootPath);
        AppendDirectoryStamp(builder, Path.Combine(rootPath, "About"));
        AppendDirectoryStamp(builder, Path.Combine(rootPath, "Assemblies"));
        AppendDirectoryStamp(builder, Path.Combine(rootPath, "Defs"));
        AppendDirectoryStamp(builder, Path.Combine(rootPath, "Patches"));
        AppendDirectoryStamp(builder, Path.Combine(rootPath, "Languages"));
        AppendDirectoryStamp(builder, Path.Combine(rootPath, "Textures"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendPathStamp(StringBuilder builder, string path)
    {
        try
        {
            var info = new FileInfo(path);
            builder.Append('|').Append(path).Append(':').Append(info.Exists ? info.Length : -1).Append(':').Append(info.Exists ? info.LastWriteTimeUtc.Ticks : 0);
        }
        catch { builder.Append('|').Append(path).Append(":error"); }
    }

    private static void AppendDirectoryStamp(StringBuilder builder, string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            builder.Append('|').Append(path).Append(':').Append(info.Exists ? info.LastWriteTimeUtc.Ticks : 0);
        }
        catch { builder.Append('|').Append(path).Append(":error"); }
    }

    private static async Task<EvidenceCacheReadResult> TryReadCacheAsync(
        string cachePath,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(cachePath))
        {
            return new EvidenceCacheReadResult(null, ModEvidenceCacheStatus.Miss);
        }

        try
        {
            await using var stream = new FileStream(
                cachePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var entry = await JsonSerializer.DeserializeAsync<EvidenceCacheEntry>(
                stream,
                CacheJsonOptions,
                cancellationToken).ConfigureAwait(false);

            if (entry is null)
            {
                QuarantineCorruptCache(cachePath);
                return new EvidenceCacheReadResult(null, ModEvidenceCacheStatus.Corrupt);
            }

            if (entry.SchemaVersion != CacheSchemaVersion)
            {
                return new EvidenceCacheReadResult(null, ModEvidenceCacheStatus.SchemaMismatch);
            }

            if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return new EvidenceCacheReadResult(null, ModEvidenceCacheStatus.FingerprintMismatch);
            }

            return new EvidenceCacheReadResult(entry.Evidence, ModEvidenceCacheStatus.Hit);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            QuarantineCorruptCache(cachePath);
            return new EvidenceCacheReadResult(null, ModEvidenceCacheStatus.Corrupt);
        }
    }

    private static async Task WriteCacheAsync(
        string cachePath,
        string rootPath,
        string targetRimWorldVersion,
        string fingerprint,
        ModEvidence evidence,
        CancellationToken cancellationToken)
    {
        var tempPath = cachePath + ".tmp";
        try
        {
            var entry = new EvidenceCacheEntry(
                CacheSchemaVersion,
                rootPath,
                targetRimWorldVersion,
                fingerprint,
                DateTimeOffset.UtcNow,
                evidence);
            await using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, entry, CacheJsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, cachePath, true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Cache failures must never block discovery.
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch { }
        }
    }

    private static void QuarantineCorruptCache(string cachePath)
    {
        try
        {
            var quarantinePath = cachePath + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(cachePath, quarantinePath, true);
        }
        catch
        {
            try { File.Delete(cachePath); } catch { }
        }
    }


    private static string NormalizeTargetVersion(string? version)
    {
        var match = Regex.Match(version ?? string.Empty, @"1\.[0-9]+", RegexOptions.CultureInvariant);
        return match.Success ? match.Value : "1.6";
    }

    private static bool TryGetVersionFolder(string directoryName, out string version)
    {
        version = string.Empty;
        var normalized = directoryName.Trim();
        var match = Regex.Match(
            normalized,
            @"^(?:(?:v(?:ersion)?|rimworld|rw)[ _-]*)?(1\.[0-9]+)(?:\.0)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return false;
        version = match.Groups[1].Value;
        return true;
    }

    private static string HashPath(string path) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())));
    private static bool IsUnderFolder(string path, string folder) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part.Equals(folder, StringComparison.OrdinalIgnoreCase));
    private static long SafeLength(string path) { try { return new FileInfo(path).Length; } catch { return 0; } }
    private static void Add(Dictionary<ModEvidenceKind, int> counters, ModEvidenceKind kind) => counters[kind] = counters.TryGetValue(kind, out var count) ? count + 1 : 1;

    private static void IncrementCapabilityCounter(Dictionary<ModEvidenceKind, int> counters, string defName)
    {
        if (defName.Contains("Faction", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Factions);
        if (defName.Contains("Biome", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Biomes);
        if (defName.Contains("Research", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Research);
        if (defName.Contains("Recipe", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Recipes);
        if (defName.Contains("Thing", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Buildings);
        if (defName.Contains("Apparel", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Apparel);
        if (defName.Contains("Weapon", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Weapons);
        if (defName.Contains("PawnKind", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.PawnKinds);
        if (defName.Contains("Hediff", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Hediffs);
        if (defName.Contains("Incident", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Incidents);
        if (defName.Contains("Job", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Jobs);
        if (defName.Contains("ThinkTree", StringComparison.OrdinalIgnoreCase) || defName.Contains("ThinkNode", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.ArtificialIntelligence);
        if (defName.Contains("World", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.WorldGeneration);
        if (defName.Contains("Scenario", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Scenarios);
        if (defName.Contains("Plant", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Plants);
        if (defName.Contains("Animal", StringComparison.OrdinalIgnoreCase)) Add(counters, ModEvidenceKind.Animals);
    }

    private static IEnumerable<string> BuildCapabilities(IReadOnlyDictionary<ModEvidenceKind, int> counters)
    {
        if (counters.ContainsKey(ModEvidenceKind.Factions)) yield return "Adds or modifies factions";
        if (counters.ContainsKey(ModEvidenceKind.Research)) yield return "Extends the research tree";
        if (counters.ContainsKey(ModEvidenceKind.Weapons)) yield return "Adds or modifies weapons";
        if (counters.ContainsKey(ModEvidenceKind.Buildings)) yield return "Adds or modifies buildable things";
        if (counters.ContainsKey(ModEvidenceKind.ArtificialIntelligence)) yield return "Changes pawn or AI behavior";
        if (counters.ContainsKey(ModEvidenceKind.WorldGeneration)) yield return "Changes world generation";
        if (counters.ContainsKey(ModEvidenceKind.Incidents)) yield return "Adds storyteller incidents or events";
        if (counters.ContainsKey(ModEvidenceKind.Hediffs)) yield return "Adds health conditions or body effects";
        if (counters.ContainsKey(ModEvidenceKind.PatchOperations)) yield return "Patches existing mod or vanilla definitions";
        if (counters.ContainsKey(ModEvidenceKind.CSharp)) yield return "Contains compiled gameplay code";
    }

    private static ModEvidenceBadge CreateBadge(
        ModEvidenceKind kind,
        int count,
        string rootPath,
        IReadOnlyList<string> xmlFiles,
        IReadOnlyList<string> assemblyFiles,
        IReadOnlyList<string> textureFiles,
        IReadOnlyList<string> audioFiles,
        IReadOnlyList<string> languageFiles,
        int defs,
        int patches)
    {
        var label = kind switch
        {
            ModEvidenceKind.CSharp => "C#", ModEvidenceKind.Xml => "XML", ModEvidenceKind.Definitions => "Defs", ModEvidenceKind.Harmony => "Harmony",
            ModEvidenceKind.Textures => "Textures", ModEvidenceKind.Audio => "Audio", ModEvidenceKind.Languages => "Lang", ModEvidenceKind.Assemblies => "DLL",
            ModEvidenceKind.PatchOperations => "PatchOps", ModEvidenceKind.ArtificialIntelligence => "AI", ModEvidenceKind.WorldGeneration => "World",
            ModEvidenceKind.PawnKinds => "Pawns", ModEvidenceKind.Incidents => "Events", ModEvidenceKind.SaveData => "Save", _ => kind.ToString()
        };

        IReadOnlyList<string> sourceFiles = kind switch
        {
            ModEvidenceKind.CSharp or ModEvidenceKind.Assemblies or ModEvidenceKind.Harmony => assemblyFiles,
            ModEvidenceKind.Xml or ModEvidenceKind.Definitions or ModEvidenceKind.PatchOperations or
            ModEvidenceKind.Scenarios or ModEvidenceKind.Biomes or ModEvidenceKind.Factions or ModEvidenceKind.Research or
            ModEvidenceKind.Recipes or ModEvidenceKind.Buildings or ModEvidenceKind.Apparel or ModEvidenceKind.Weapons or
            ModEvidenceKind.Animals or ModEvidenceKind.Plants or ModEvidenceKind.PawnKinds or ModEvidenceKind.Incidents or
            ModEvidenceKind.Hediffs or ModEvidenceKind.Jobs or ModEvidenceKind.ArtificialIntelligence or ModEvidenceKind.WorldGeneration => xmlFiles,
            ModEvidenceKind.Textures => textureFiles,
            ModEvidenceKind.Audio => audioFiles,
            ModEvidenceKind.Languages => languageFiles,
            _ => Array.Empty<string>()
        };

        var relativeFiles = sourceFiles
            .Select(path => Path.GetRelativePath(rootPath, path).Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var summary = kind switch
        {
            ModEvidenceKind.CSharp => $"{assemblyFiles.Count} managed assembly file(s)",
            ModEvidenceKind.Xml => $"{xmlFiles.Count} XML file(s)",
            ModEvidenceKind.Definitions => $"{defs} definition element(s)",
            ModEvidenceKind.PatchOperations => $"{patches} patch operation(s)",
            ModEvidenceKind.Textures => $"{textureFiles.Count} texture asset(s)",
            ModEvidenceKind.Audio => $"{audioFiles.Count} audio asset(s)",
            ModEvidenceKind.Languages => $"{languageFiles.Count} translation file(s)",
            _ => $"{count} discovered item(s)"
        };
        var details = $"Discovered from the mod's installed file tree and XML definition structure. {relativeFiles.Length:N0} matching file(s) indexed.";
        return new ModEvidenceBadge(kind, label, count, summary, details, relativeFiles);
    }

    private static int BadgePriority(ModEvidenceKind kind) => kind switch
    {
        ModEvidenceKind.Factions or ModEvidenceKind.Weapons or ModEvidenceKind.Apparel or ModEvidenceKind.Animals or
        ModEvidenceKind.Plants or ModEvidenceKind.Buildings or ModEvidenceKind.Research or ModEvidenceKind.WorldGeneration or
        ModEvidenceKind.Scenarios or ModEvidenceKind.ArtificialIntelligence or ModEvidenceKind.Hediffs or ModEvidenceKind.Jobs or
        ModEvidenceKind.Incidents or ModEvidenceKind.PawnKinds or ModEvidenceKind.Biomes or ModEvidenceKind.Recipes => 0,
        ModEvidenceKind.Harmony or ModEvidenceKind.CSharp or ModEvidenceKind.PatchOperations or ModEvidenceKind.SaveData => 10,
        ModEvidenceKind.Xml or ModEvidenceKind.Definitions or ModEvidenceKind.Textures or ModEvidenceKind.Audio or
        ModEvidenceKind.Languages or ModEvidenceKind.Assemblies => 20,
        _ => 30
    };

    private sealed record EvidenceCacheReadResult(ModEvidence? Evidence, ModEvidenceCacheStatus Status);

    private sealed record EvidenceCacheEntry(
        int SchemaVersion,
        string RootPath,
        string TargetRimWorldVersion,
        string Fingerprint,
        DateTimeOffset ScannedAtUtc,
        ModEvidence Evidence);
}
