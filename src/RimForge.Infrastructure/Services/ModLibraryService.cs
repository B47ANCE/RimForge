using System.Collections.Concurrent;
using System.Diagnostics;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class ModLibraryService : IModLibraryService
{
    private readonly IConfigurationService _configurationService;
    private readonly IAboutXmlParser _aboutXmlParser;
    private readonly IDependencyGraphService _dependencyGraphService;
    private readonly ISteamLibraryDiscoveryService _steamLibraryDiscoveryService;

    public ModLibraryService(
        IConfigurationService configurationService,
        IAboutXmlParser aboutXmlParser,
        IDependencyGraphService dependencyGraphService,
        ISteamLibraryDiscoveryService steamLibraryDiscoveryService)
    {
        _configurationService = configurationService;
        _aboutXmlParser = aboutXmlParser;
        _dependencyGraphService = dependencyGraphService;
        _steamLibraryDiscoveryService = steamLibraryDiscoveryService;
    }

    public async Task<ModLibrarySnapshot> ScanAsync(
        string repositoryRoot,
        IProgress<ForgeProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<ModRecord>? discoveredModProgress = null,
        bool includeEvidence = true)
    {
        var serviceStopwatch = Stopwatch.StartNew();
        progress?.Report(new ForgeProgress(
            ForgePhase.Configuration,
            "Loading RimForge configuration...",
            0.01,
            0.15));

        var configuration = await _configurationService
            .LoadAsync(repositoryRoot, cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(new ForgeProgress(
            ForgePhase.Discovery,
            "Discovering official, Workshop, and local content...",
            0.04,
            0));

        var discoveryStopwatch = Stopwatch.StartNew();
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var discoveryRoots = BuildDiscoveryRoots(configuration, repositoryRoot);
        var configuredRoots = discoveryRoots.Count;
        var rootsCompleted = 0;

        foreach (var root in discoveryRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var discoveredInRoot = 0;
            foreach (var folder in EnumerateModFolders(root))
            {
                if (File.Exists(Path.Combine(folder, "About", "About.xml")) && folders.Add(folder))
                {
                    discoveredInRoot++;
                }
            }

            rootsCompleted++;
            var rootFraction = configuredRoots == 0 ? 1d : rootsCompleted / (double)configuredRoots;
            progress?.Report(new ForgeProgress(
                ForgePhase.Discovery,
                Directory.Exists(root)
                    ? $"Scanned {root} ({discoveredInRoot} item(s))"
                    : $"Skipped unavailable discovery root: {root}",
                0.04 + (0.06 * rootFraction),
                rootFraction,
                rootsCompleted,
                configuredRoots));
        }

        var orderedFolders = folders.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        discoveryStopwatch.Stop();
        var libraryCache = new NativeLibraryCache(repositoryRoot, configuration);
        var loadStopwatch = Stopwatch.StartNew();
        var cacheLoad = await libraryCache.LoadAsync(cancellationToken).ConfigureAwait(false);
        loadStopwatch.Stop();
        var cachedEntries = cacheLoad.Entries;
        var parsed = new ConcurrentBag<ModRecord>();
        var cacheEntries = new ConcurrentBag<NativeLibraryCacheEntry>();
        var changedFolders = new List<(string Folder, NativeLibrarySignature Signature)>();
        var completed = 0;
        var evidenceCacheHits = 0;
        var libraryCacheHits = 0;
        var missReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var signatureStopwatch = Stopwatch.StartNew();

        foreach (var rawFolder in orderedFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = NativeLibraryCache.NormalizePath(rawFolder);
            var signature = NativeLibraryCache.CreateSignature(folder, configuration.TargetRimWorldVersion);
            if (cachedEntries.TryGetValue(folder, out var cached) && cached.Signature == signature)
            {
                parsed.Add(cached.Mod);
                cacheEntries.Add(cached);
                discoveredModProgress?.Report(cached.Mod);
                libraryCacheHits++;
                completed++;
            }
            else
            {
                var reason = cachedEntries.TryGetValue(folder, out cached)
                    ? NativeLibraryCache.GetMissReason(cached.Signature, signature)
                    : "NotCached";
                missReasons[reason] = missReasons.GetValueOrDefault(reason) + 1;
                changedFolders.Add((folder, signature));
            }
        }
        signatureStopwatch.Stop();

        progress?.Report(new ForgeProgress(
            ForgePhase.AboutParsing,
            libraryCacheHits > 0
                ? $"Loaded {libraryCacheHits} cached mod(s); refreshing {changedFolders.Count} changed item(s)..."
                : $"Reading About.xml metadata for {orderedFolders.Length} mods...",
            0.10,
            orderedFolders.Length == 0 ? 1 : completed / (double)orderedFolders.Length,
            completed,
            orderedFolders.Length));

        var parseStopwatch = Stopwatch.StartNew();
        await Parallel.ForEachAsync(
            changedFolders,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount - 1, 2, 8)
            },
            async (workItem, token) =>
            {
                var folder = workItem.Folder;
                ModRecord mod;
                var cacheHit = false;
                try
                {
                    mod = await _aboutXmlParser.ParseAsync(folder, token).ConfigureAwait(false);

                    // Publish local metadata as soon as About.xml has been parsed. The UI can
                    // display the installed mod immediately with a pending health state while
                    // Forge Evidence continues in the background. The final snapshot replaces
                    // these preliminary records after enrichment and analysis complete.
                    discoveredModProgress?.Report(mod);

                    if (mod.IsOfficialContent || !includeEvidence)
                    {
                        // Startup discovery intentionally stops at local metadata. Deep Evidence
                        // inspection is a separate background intelligence phase so first use is
                        // never blocked by assemblies, Defs, patches, textures, or other assets.
                        mod.Evidence = ModEvidence.Empty;
                    }
                    else
                    {
                        var evidenceResult = await ModEvidenceScanner.ScanOrLoadAsync(
                            folder,
                            repositoryRoot,
                            configuration.TargetRimWorldVersion,
                            token).ConfigureAwait(false);
                        mod.Evidence = evidenceResult.Evidence;
                        cacheHit = evidenceResult.CacheHit;
                        if (cacheHit) Interlocked.Increment(ref evidenceCacheHits);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // A single inaccessible or malformed mod must not abort the entire library.
                    var fullPath = Path.GetFullPath(folder);
                    var folderName = Path.GetFileName(fullPath.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));
                    mod = new ModRecord
                    {
                        Id = fullPath,
                        RootPath = fullPath,
                        FolderName = folderName,
                        AboutPath = Path.Combine(fullPath, "About", "About.xml"),
                        Name = folderName,
                        LastModified = Directory.Exists(fullPath)
                            ? Directory.GetLastWriteTimeUtc(fullPath)
                            : DateTime.MinValue,
                        Evidence = ModEvidence.Empty,
                        Errors = new[] { $"Discovery failed for this item: {ex.Message}" }
                    };
                }

                parsed.Add(mod);
                cacheEntries.Add(new NativeLibraryCacheEntry(folder, workItem.Signature, mod));
                discoveredModProgress?.Report(mod);

                var current = Interlocked.Increment(ref completed);
                var fraction = orderedFolders.Length == 0 ? 1d : current / (double)orderedFolders.Length;
                progress?.Report(new ForgeProgress(
                    ForgePhase.AboutParsing,
                    mod.IsOfficialContent
                        ? $"Loaded official content: {mod.DisplayName}"
                        : mod.Errors.Any(error => error.StartsWith("Discovery failed", StringComparison.OrdinalIgnoreCase))
                            ? $"Loaded {mod.DisplayName} with discovery warnings"
                            : cacheHit
                                ? $"Loaded cached evidence: {mod.DisplayName}"
                                : $"Scanning {mod.DisplayName}\'s About.xml, path, and assembly metadata for object evidence.",
                    0.10 + (0.60 * fraction),
                    fraction,
                    current,
                    orderedFolders.Length));
            }).ConfigureAwait(false);
        parseStopwatch.Stop();

        var materializationStopwatch = Stopwatch.StartNew();
        var mods = parsed
            .OrderBy(mod => mod.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        materializationStopwatch.Stop();

        var saveStopwatch = Stopwatch.StartNew();
        try
        {
            await libraryCache.SaveAsync(cacheEntries, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            progress?.Report(new ForgeProgress(
                ForgePhase.IndexBuilding,
                $"Native library cache could not be updated: {ex.Message}",
                0.72,
                1,
                mods.Length,
                mods.Length));
        }
        finally
        {
            saveStopwatch.Stop();
        }

        progress?.Report(new ForgeProgress(
            ForgePhase.Validation,
            "Validating names, package IDs, Workshop IDs, and supported versions...",
            0.74,
            0.25,
            mods.Length,
            mods.Length));
        var validationStopwatch = Stopwatch.StartNew();
        var validation = Validate(mods);
        validationStopwatch.Stop();

        progress?.Report(new ForgeProgress(
            ForgePhase.IndexBuilding,
            "Building package and Workshop indexes...",
            0.82,
            0.65,
            mods.Length,
            mods.Length));

        progress?.Report(new ForgeProgress(
            ForgePhase.DependencyGraph,
            "Building native dependency graph and detecting cycles...",
            0.90,
            0.20,
            mods.Length,
            mods.Length));
        var graphStopwatch = Stopwatch.StartNew();
        var (graph, missing, cycles) = _dependencyGraphService.Build(mods);
        graphStopwatch.Stop();

        progress?.Report(new ForgeProgress(
            ForgePhase.Complete,
            $"Loaded {mods.Length} mods and {graph.Edges.Count} relationships. Library cache: {libraryCacheHits}/{mods.Length}; Evidence cache: {evidenceCacheHits}/{Math.Max(1, changedFolders.Count)} hit(s).",
            1,
            1,
            mods.Length,
            mods.Length));

        var cachedPathSet = cachedEntries.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var discoveredPathSet = orderedFolders
            .Select(NativeLibraryCache.NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        serviceStopwatch.Stop();
        var cacheMetrics = new NativeLibraryCacheMetrics(
            libraryCache.Path,
            cacheLoad.Status,
            cacheLoad.Error,
            discoveredPathSet.Count,
            cachedEntries.Count,
            libraryCacheHits,
            changedFolders.Count,
            discoveredPathSet.Count(path => !cachedPathSet.Contains(path)),
            cachedPathSet.Count(path => !discoveredPathSet.Contains(path)),
            changedFolders.Count,
            loadStopwatch.Elapsed.TotalMilliseconds,
            signatureStopwatch.Elapsed.TotalMilliseconds,
            parseStopwatch.Elapsed.TotalMilliseconds,
            saveStopwatch.Elapsed.TotalMilliseconds,
            missReasons
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new NativeLibraryCacheMissReason(pair.Key, pair.Value))
                .ToArray())
        {
            DiscoveryMilliseconds = discoveryStopwatch.Elapsed.TotalMilliseconds,
            MaterializationMilliseconds = materializationStopwatch.Elapsed.TotalMilliseconds,
            ValidationMilliseconds = validationStopwatch.Elapsed.TotalMilliseconds,
            DependencyGraphMilliseconds = graphStopwatch.Elapsed.TotalMilliseconds,
            ServiceTotalMilliseconds = serviceStopwatch.Elapsed.TotalMilliseconds
        };

        return new ModLibrarySnapshot(mods, validation, graph, missing, cycles, DateTimeOffset.Now)
        {
            CacheMetrics = cacheMetrics
        };
    }


    public async Task<int> EnrichEvidenceAsync(
        string repositoryRoot,
        IReadOnlyList<ModRecord> mods,
        IProgress<ForgeProgress>? progress = null,
        IProgress<ModRecord>? enrichedModProgress = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = await _configurationService
            .LoadAsync(repositoryRoot, cancellationToken)
            .ConfigureAwait(false);
        var candidates = mods.Where(mod => !mod.IsOfficialContent).ToArray();
        var completed = 0;

        await Parallel.ForEachAsync(
            candidates,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount - 1, 2, 6)
            },
            async (mod, token) =>
            {
                try
                {
                    var result = await ModEvidenceScanner.ScanOrLoadAsync(
                        mod.RootPath,
                        repositoryRoot,
                        configuration.TargetRimWorldVersion,
                        token).ConfigureAwait(false);
                    mod.Evidence = result.Evidence;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Intelligence enrichment is optional. A single unreadable mod must not
                    // invalidate the already-usable metadata library.
                    mod.Evidence = ModEvidence.Empty;
                }

                enrichedModProgress?.Report(mod);

                var current = Interlocked.Increment(ref completed);
                var fraction = candidates.Length == 0 ? 1d : current / (double)candidates.Length;
                progress?.Report(new ForgeProgress(
                    ForgePhase.EvidenceScan,
                    $"Building background intelligence: {current}/{candidates.Length}",
                    fraction,
                    fraction,
                    current,
                    candidates.Length));
            }).ConfigureAwait(false);

        return completed;
    }

    private IReadOnlyList<string> BuildDiscoveryRoots(
        RimForgeConfiguration configuration,
        string repositoryRoot)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuredRoot in configuration.RootFolders)
        {
            if (string.IsNullOrWhiteSpace(configuredRoot)) continue;

            try
            {
                var root = Environment.ExpandEnvironmentVariables(configuredRoot);
                if (!Path.IsPathRooted(root))
                {
                    root = Path.GetFullPath(Path.Combine(repositoryRoot, root));
                }
                else
                {
                    root = Path.GetFullPath(root);
                }

                roots.Add(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                AddInferredOfficialContentRoot(roots, root);
            }
            catch
            {
                // Preserve the existing tolerant discovery behavior for malformed optional roots.
            }
        }

        AddConfiguredGameInstallationRoot(roots, configuration.RimWorldExecutable);

        // Configured roots are hints, not the source of truth. Steam Workshop content may
        // be installed in a different Steam library than RimWorld itself, so enumerate all
        // Steam libraries and add each valid RimWorld content root independently.
        IReadOnlyList<SteamInstallationCandidate> candidates;
        try
        {
            candidates = _steamLibraryDiscoveryService.FindRimWorldInstallations();
        }
        catch
        {
            candidates = Array.Empty<SteamInstallationCandidate>();
        }

        foreach (var candidate in candidates)
        {
            AddExistingRoot(roots, candidate.WorkshopFolder);
            AddExistingRoot(roots, candidate.LocalModsFolder);
            AddExistingRoot(roots, candidate.OfficialContentFolder);
        }

        return roots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> EnumerateModFolders(string root)
    {
        if (!Directory.Exists(root)) return Array.Empty<string>();

        try
        {
            return Directory.GetDirectories(root);
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }

    private static void AddExistingRoot(ISet<string> roots, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Directory.Exists(fullPath)) roots.Add(fullPath);
        }
        catch
        {
            // Optional discovery roots must never abort the complete library scan.
        }
    }

    private static void AddConfiguredGameInstallationRoot(ISet<string> roots, string? rimWorldExecutable)
    {
        if (string.IsNullOrWhiteSpace(rimWorldExecutable)) return;

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(rimWorldExecutable);
            var fullPath = Path.GetFullPath(expanded);
            var gameRoot = Directory.Exists(fullPath)
                ? fullPath
                : Path.GetDirectoryName(fullPath);

            AddOfficialDataRoot(roots, gameRoot);
        }
        catch
        {
            // The configured executable is optional. Malformed values must not block normal discovery.
        }
    }

    private static void AddInferredOfficialContentRoot(ISet<string> roots, string configuredRoot)
    {
        var normalized = configuredRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Standard local-mod layout: <RimWorld>/Mods -> <RimWorld>/Data.
        if (Path.GetFileName(normalized).Equals("Mods", StringComparison.OrdinalIgnoreCase))
        {
            var gameRoot = Directory.GetParent(normalized)?.FullName;
            AddOfficialDataRoot(roots, gameRoot);
        }

        // Standard Workshop layout:
        // <library>/steamapps/workshop/content/294100 -> <library>/steamapps/common/RimWorld/Data.
        var directory = new DirectoryInfo(normalized);
        if (directory.Name.Equals("294100", StringComparison.OrdinalIgnoreCase) &&
            directory.Parent?.Name.Equals("content", StringComparison.OrdinalIgnoreCase) == true &&
            directory.Parent.Parent?.Name.Equals("workshop", StringComparison.OrdinalIgnoreCase) == true &&
            directory.Parent.Parent.Parent?.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase) == true)
        {
            var steamAppsRoot = directory.Parent.Parent.Parent.FullName;
            AddOfficialDataRoot(roots, Path.Combine(steamAppsRoot, "common", "RimWorld"));
        }
    }

    private static void AddOfficialDataRoot(ISet<string> roots, string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot)) return;

        var dataRoot = Path.Combine(gameRoot, "Data");
        if (Directory.Exists(dataRoot))
        {
            roots.Add(Path.GetFullPath(dataRoot));
        }
    }

    private static ModValidationSummary Validate(IReadOnlyList<ModRecord> mods)
    {
        var duplicatePackages = mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
            .GroupBy(mod => mod.PackageId!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var duplicateWorkshopIds = mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.WorkshopId))
            .GroupBy(mod => mod.WorkshopId!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ModValidationSummary(
            mods.Count(mod => string.IsNullOrWhiteSpace(mod.Name)),
            mods.Count(mod => string.IsNullOrWhiteSpace(mod.PackageId)),
            duplicatePackages.Length,
            duplicateWorkshopIds.Length,
            duplicatePackages,
            duplicateWorkshopIds);
    }
}
