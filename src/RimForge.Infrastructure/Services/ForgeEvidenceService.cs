using System.Collections.Concurrent;
using RimForge.Core.Models;

namespace RimForge.Infrastructure.Services;

public enum ForgeEvidenceInvalidationReason
{
    Manual,
    FileChanged,
    ModAdded,
    ModRemoved,
    TargetVersionChanged,
    CacheCorrupt,
    WatcherOverflow,
    RuntimeEvidenceChanged
}


public sealed record ForgeEvidenceEntry(
    string ModId,
    string PackageId,
    string RootPath,
    string TargetRimWorldVersion,
    string Fingerprint,
    ModEvidence Evidence,
    bool CacheHit,
    DateTimeOffset ScannedAtUtc,
    TimeSpan Elapsed,
    IReadOnlyList<ForgeEvidenceProvenance>? Provenance = null)
{
    public IReadOnlyList<ForgeEvidenceProvenance> EffectiveProvenance { get; } =
        Provenance ?? Array.Empty<ForgeEvidenceProvenance>();
}

public sealed record ForgeEvidenceMetrics(
    int Generation,
    int Requested,
    int Scanned,
    int Reused,
    int CacheMisses,
    int CorruptRecovered,
    int CoalescedRequests,
    int DebouncedInvalidations,
    int WatcherOverflows,
    int CacheFilesDeleted,
    int TemporaryFilesDeleted,
    int QuarantineFilesDeleted,
    int Failed,
    int Removed,
    TimeSpan Elapsed,
    bool WasCancelled,
    int PendingInvalidations = 0,
    int ReconciledContributions = 0,
    int ActiveWatchers = 0);

public sealed record ForgeEvidenceProgress(
    int Completed,
    int Total,
    string ModName,
    string RootPath,
    bool CacheHit);

public sealed class ForgeEvidenceSnapshot
{
    public static ForgeEvidenceSnapshot Empty { get; } = new(
        0,
        string.Empty,
        DateTimeOffset.MinValue,
        new Dictionary<string, ForgeEvidenceEntry>(StringComparer.OrdinalIgnoreCase),
        new ForgeEvidenceMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, TimeSpan.Zero, false),
        ForgeEvidenceSchema.CurrentVersion,
        ForgeEvidenceSchema.PlatformVersion,
        Array.Empty<ForgeEvidenceContribution>(),
        Array.Empty<ForgeEvidenceProducerDiagnostic>());

    public ForgeEvidenceSnapshot(
        int generation,
        string targetRimWorldVersion,
        DateTimeOffset publishedAtUtc,
        IReadOnlyDictionary<string, ForgeEvidenceEntry> entries,
        ForgeEvidenceMetrics metrics,
        int schemaVersion = ForgeEvidenceSchema.CurrentVersion,
        string platformVersion = ForgeEvidenceSchema.PlatformVersion,
        IReadOnlyList<ForgeEvidenceContribution>? contributions = null,
        IReadOnlyList<ForgeEvidenceProducerDiagnostic>? producerDiagnostics = null)
    {
        Generation = generation;
        TargetRimWorldVersion = targetRimWorldVersion;
        PublishedAtUtc = publishedAtUtc;
        Entries = entries;
        Metrics = metrics;
        SchemaVersion = schemaVersion;
        PlatformVersion = platformVersion;
        Contributions = contributions ?? Array.Empty<ForgeEvidenceContribution>();
        ProducerDiagnostics = producerDiagnostics ?? Array.Empty<ForgeEvidenceProducerDiagnostic>();
        Index = Contributions.Count == 0 ? ForgeEvidenceIndex.Empty : new ForgeEvidenceIndex(Contributions);
    }

    public int SchemaVersion { get; }
    public string PlatformVersion { get; }
    public IReadOnlyList<ForgeEvidenceContribution> Contributions { get; }
    public IReadOnlyList<ForgeEvidenceProducerDiagnostic> ProducerDiagnostics { get; }
    public ForgeEvidenceIndex Index { get; }
    public int Generation { get; }
    public string TargetRimWorldVersion { get; }
    public DateTimeOffset PublishedAtUtc { get; }
    public IReadOnlyDictionary<string, ForgeEvidenceEntry> Entries { get; }
    public ForgeEvidenceMetrics Metrics { get; }

    public bool TryGet(string? modId, out ForgeEvidenceEntry? entry)
    {
        if (!string.IsNullOrWhiteSpace(modId) && Entries.TryGetValue(modId, out var found))
        {
            entry = found;
            return true;
        }

        entry = null;
        return false;
    }
}

public interface IForgeEvidenceService : IAsyncDisposable
{
    ForgeEvidenceSnapshot Current { get; }
    event EventHandler<string>? Invalidated;

    Task<ForgeEvidenceSnapshot> RefreshAsync(
        IReadOnlyList<ModRecord> mods,
        string repositoryRoot,
        string targetRimWorldVersion,
        IProgress<ForgeEvidenceProgress>? progress = null,
        CancellationToken cancellationToken = default,
        bool forceRescan = false);

    Task<ForgeEvidenceIngestionResult> IngestAsync(
        ForgeEvidenceIngestionBatch batch,
        string repositoryRoot,
        CancellationToken cancellationToken = default);

    Task<ForgeEvidenceSnapshot?> RestoreAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default);

    void Invalidate(string rootPath, ForgeEvidenceInvalidationReason reason);
    void StartWatching(IEnumerable<ModRecord> mods);
    void StopWatching();
    void CancelCurrent();
}

/// <summary>
/// Coordinates all deep mod evidence work. Requests are serialized and coalesced,
/// results are published as immutable generations, and file watchers only invalidate
/// cached entries; they never perform analysis on the watcher callback thread.
/// </summary>
public sealed class ForgeEvidenceService : IForgeEvidenceService
{
    private readonly SemaphoreSlim _schedulerGate = new(1, 1);
    private readonly IForgeEvidenceStore _evidenceStore;
    private readonly IForgeEvidencePipeline _pipeline;
    private readonly IForgeEvidenceBus _bus;
    private readonly ForgeEvidenceServiceOptions _options;
    private readonly ForgeEvidenceInvalidationJournal _invalidationJournal = new();
    private readonly object _stateGate = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<ForgeEvidenceSnapshot>>> _scheduledRefreshes =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileSystemWatcher> _watchers =
        new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _activeScanCts;
    private int _generation;
    private int _coalescedRequests;
    private int _debouncedInvalidations;
    private int _watcherOverflows;
    private bool _disposed;

    public ForgeEvidenceService(
        IEnumerable<IForgeEvidenceProducer>? producers = null,
        ForgeEvidenceServiceOptions? options = null)
        : this(
            CreateDefaultStore(),
            new ForgeEvidencePipeline(producers ?? CreateDefaultProducers()),
            new ForgeEvidenceBus(),
            options ?? ForgeEvidenceServiceOptions.Default)
    {
    }

    public ForgeEvidenceService(
        IForgeEvidenceStore evidenceStore,
        IForgeEvidencePipeline pipeline,
        IForgeEvidenceBus bus,
        ForgeEvidenceServiceOptions? options = null)
    {
        _evidenceStore = evidenceStore ?? throw new ArgumentNullException(nameof(evidenceStore));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _options = options ?? ForgeEvidenceServiceOptions.Default;
        _options.Validate();
    }

    public ForgeEvidenceService(
        IForgeEvidenceBus bus,
        IEnumerable<IForgeEvidenceProducer>? producers = null,
        ForgeEvidenceServiceOptions? options = null)
        : this(
            CreateDefaultStore(),
            new ForgeEvidencePipeline(producers ?? CreateDefaultProducers()),
            bus,
            options ?? ForgeEvidenceServiceOptions.Default)
    {
    }

    private static IForgeEvidenceStore CreateDefaultStore()
    {
        var paths = RimForgePathLayout.Create(RimForgePathLayout.ResolveRepositoryRoot());
        return new ForgeEvidenceStore(Path.Combine(paths.CacheRoot, "ForgeEvidence"));
    }

    private static IReadOnlyList<IForgeEvidenceProducer> CreateDefaultProducers() =>
        ForgeEvidenceProducerFactory.Create();

    public ForgeEvidenceSnapshot Current
    {
        get => _bus.Current;
    }

    public event EventHandler<string>? Invalidated;

    public Task<ForgeEvidenceSnapshot> RefreshAsync(
        IReadOnlyList<ModRecord> mods,
        string repositoryRoot,
        string targetRimWorldVersion,
        IProgress<ForgeEvidenceProgress>? progress = null,
        CancellationToken cancellationToken = default,
        bool forceRescan = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var signature = BuildRequestSignature(mods, repositoryRoot, targetRimWorldVersion, forceRescan);
        var candidate = new Lazy<Task<ForgeEvidenceSnapshot>>(
            () => RunScheduledRefreshAsync(mods, repositoryRoot, targetRimWorldVersion, progress, forceRescan),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var scheduled = _scheduledRefreshes.GetOrAdd(signature, candidate);
        if (!ReferenceEquals(candidate, scheduled))
            Interlocked.Increment(ref _coalescedRequests);

        var refresh = scheduled.Value;
        _ = refresh.ContinueWith(
            (completedTask, state) =>
            {
                var tuple = ((ConcurrentDictionary<string, Lazy<Task<ForgeEvidenceSnapshot>>>, string, Lazy<Task<ForgeEvidenceSnapshot>>))state!;
                if (tuple.Item1.TryGetValue(tuple.Item2, out var current) && ReferenceEquals(current, tuple.Item3))
                    tuple.Item1.TryRemove(tuple.Item2, out _);
            },
            (_scheduledRefreshes, signature, scheduled),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return cancellationToken.CanBeCanceled
            ? refresh.WaitAsync(cancellationToken)
            : refresh;
    }

    private async Task<ForgeEvidenceSnapshot> RunScheduledRefreshAsync(
        IReadOnlyList<ModRecord> mods,
        string repositoryRoot,
        string targetRimWorldVersion,
        IProgress<ForgeEvidenceProgress>? progress,
        bool forceRescan)
    {
        await _schedulerGate.WaitAsync().ConfigureAwait(false);
        var started = DateTimeOffset.UtcNow;
        using var scanCts = new CancellationTokenSource();
        lock (_stateGate) _activeScanCts = scanCts;

        try
        {
            var previous = Current;
            var capturedInvalidations = _invalidationJournal.Capture();
            var invalidatedRootPaths = capturedInvalidations
                .Select(value => value.RootPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var targetChanged = !string.Equals(
                previous.TargetRimWorldVersion,
                targetRimWorldVersion,
                StringComparison.OrdinalIgnoreCase);
            var candidates = mods
                .Where(mod => !mod.IsOfficialContent && Directory.Exists(mod.RootPath))
                .GroupBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
            var candidateIds = candidates.Select(mod => mod.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removed = previous.Entries.Keys.Count(id => !candidateIds.Contains(id));
            var entries = new ConcurrentDictionary<string, ForgeEvidenceEntry>(StringComparer.OrdinalIgnoreCase);
            var scanned = 0;
            var reused = 0;
            var cacheMisses = 0;
            var corruptRecovered = 0;
            var failed = 0;
            var completed = 0;

            await Parallel.ForEachAsync(
                candidates,
                new ParallelOptions
                {
                    CancellationToken = scanCts.Token,
                    MaxDegreeOfParallelism = _options.MaximumParallelScans
                },
                async (mod, token) =>
                {
                    var invalidated = forceRescan || targetChanged || invalidatedRootPaths.Contains(Path.GetFullPath(mod.RootPath));

                    try
                    {
                        var result = await ModEvidenceScanner.ScanOrLoadAsync(
                            mod.RootPath,
                            repositoryRoot,
                            targetRimWorldVersion,
                            token,
                            invalidated).ConfigureAwait(false);
                        mod.Evidence = result.Evidence;
                        entries[mod.Id] = new ForgeEvidenceEntry(
                            mod.Id,
                            mod.PackageId ?? string.Empty,
                            mod.RootPath,
                            targetRimWorldVersion,
                            result.Fingerprint,
                            result.Evidence,
                            result.CacheHit,
                            DateTimeOffset.UtcNow,
                            result.Elapsed);
                        if (result.CacheHit)
                        {
                            Interlocked.Increment(ref reused);
                        }
                        else
                        {
                            Interlocked.Increment(ref scanned);
                            Interlocked.Increment(ref cacheMisses);
                            if (result.CacheStatus == ModEvidenceCacheStatus.Corrupt)
                                Interlocked.Increment(ref corruptRecovered);
                        }
                        var current = Interlocked.Increment(ref completed);
                        progress?.Report(new ForgeEvidenceProgress(
                            current, candidates.Length, mod.DisplayName, mod.RootPath, result.CacheHit));
                    }
                    catch (OperationCanceledException) { throw; }
                    catch
                    {
                        Interlocked.Increment(ref failed);
                        var current = Interlocked.Increment(ref completed);
                        progress?.Report(new ForgeEvidenceProgress(
                            current, candidates.Length, mod.DisplayName, mod.RootPath, false));
                    }
                }).ConfigureAwait(false);

            scanCts.Token.ThrowIfCancellationRequested();
            var collectionContext = new ForgeEvidenceCollectionContext(
                candidates,
                targetRimWorldVersion,
                Describe(previous),
                invalidatedRootPaths,
                forceRescan,
                started);
            var pipelineResult = await _pipeline.CollectAsync(
                    collectionContext,
                    progress: null,
                    cancellationToken: scanCts.Token)
                .ConfigureAwait(false);
            var contributions = ForgeEvidenceContributionReconciler.ReconcileRefresh(
                previous.Contributions,
                pipelineResult.Contributions,
                pipelineResult.CompletedSourceKinds,
                out var reconciledContributions);

            var cleanup = await ModEvidenceScanner.CleanupCacheAsync(
                repositoryRoot,
                candidates.Select(mod => mod.RootPath),
                targetRimWorldVersion,
                scanCts.Token).ConfigureAwait(false);
            scanCts.Token.ThrowIfCancellationRequested();

            var generation = Interlocked.Increment(ref _generation);
            var metrics = new ForgeEvidenceMetrics(
                generation,
                candidates.Length,
                scanned,
                reused,
                cacheMisses,
                corruptRecovered,
                Interlocked.Exchange(ref _coalescedRequests, 0),
                Interlocked.Exchange(ref _debouncedInvalidations, 0),
                Interlocked.Exchange(ref _watcherOverflows, 0),
                cleanup.CacheFilesDeleted,
                cleanup.TemporaryFilesDeleted,
                cleanup.QuarantineFilesDeleted,
                failed,
                removed,
                DateTimeOffset.UtcNow - started,
                false,
                capturedInvalidations.Count,
                reconciledContributions,
                GetWatcherCount());
            var snapshot = new ForgeEvidenceSnapshot(
                generation,
                targetRimWorldVersion,
                DateTimeOffset.UtcNow,
                new Dictionary<string, ForgeEvidenceEntry>(entries, StringComparer.OrdinalIgnoreCase),
                metrics,
                ForgeEvidenceSchema.CurrentVersion,
                ForgeEvidenceSchema.PlatformVersion,
                contributions,
                pipelineResult.Diagnostics);

            await _evidenceStore.SaveAsync(snapshot, scanCts.Token).ConfigureAwait(false);
            lock (_stateGate)
            {
                // Publication and CancelCurrent share this gate. A cancellation that wins
                // the gate prevents partial/stale generation publication; a publication
                // that wins is complete, persisted, and immutable before cancellation proceeds.
                scanCts.Token.ThrowIfCancellationRequested();
                _bus.Publish(snapshot, ForgeEvidencePublicationReason.Refreshed);
            }
            _invalidationJournal.Acknowledge(capturedInvalidations);
            return snapshot;
        }
        finally
        {
            lock (_stateGate)
            {
                if (ReferenceEquals(_activeScanCts, scanCts)) _activeScanCts = null;
            }
            _schedulerGate.Release();
        }
    }

    private static string BuildRequestSignature(
        IReadOnlyList<ModRecord> mods,
        string repositoryRoot,
        string targetRimWorldVersion,
        bool forceRescan)
    {
        var identities = mods
            .Where(mod => !mod.IsOfficialContent)
            .Select(mod => $"{mod.Id}|{Path.GetFullPath(mod.RootPath)}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        return $"{Path.GetFullPath(repositoryRoot)}::{targetRimWorldVersion}::{(forceRescan ? "force" : "cached")}::{string.Join(";;", identities)}";
    }

    public async Task<ForgeEvidenceIngestionResult> IngestAsync(
        ForgeEvidenceIngestionBatch batch,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var errors = _pipeline.ValidateBatch(batch);
        if (errors.Count > 0)
            return new ForgeEvidenceIngestionResult(batch.BatchId, 0, 0, batch.Contributions?.Count ?? 0, errors);

        await _schedulerGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = Current;
            var merged = ForgeEvidenceContributionMerger.Merge(current.Contributions, batch.Contributions, out var mergedCount);
            var generation = Interlocked.Increment(ref _generation);
            var snapshot = new ForgeEvidenceSnapshot(
                generation,
                current.TargetRimWorldVersion,
                DateTimeOffset.UtcNow,
                current.Entries,
                current.Metrics with { Generation = generation },
                ForgeEvidenceSchema.CurrentVersion,
                ForgeEvidenceSchema.PlatformVersion,
                merged,
                current.ProducerDiagnostics);

            await _evidenceStore.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            _bus.Publish(snapshot, ForgeEvidencePublicationReason.Ingested);
            return new ForgeEvidenceIngestionResult(
                batch.BatchId,
                batch.Contributions.Count,
                mergedCount,
                0,
                Array.Empty<string>());
        }
        finally
        {
            _schedulerGate.Release();
        }
    }

    public async Task<ForgeEvidenceSnapshot?> RestoreAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var loadResult = await _evidenceStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var restored = loadResult.Snapshot;
        if (restored is null) return null;

        lock (_stateGate)
        {
            _generation = Math.Max(_generation, restored.Generation);
        }
        _bus.Publish(restored, ForgeEvidencePublicationReason.Restored);
        return restored;
    }

    private static ForgeEvidenceSnapshotDescriptor Describe(ForgeEvidenceSnapshot snapshot) =>
        new(
            snapshot.Generation,
            snapshot.SchemaVersion,
            snapshot.PlatformVersion,
            snapshot.TargetRimWorldVersion,
            snapshot.PublishedAtUtc,
            snapshot.Entries.Count,
            snapshot.Contributions.Count);

    public void Invalidate(string rootPath, ForgeEvidenceInvalidationReason reason)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) return;
        var fullPath = Path.GetFullPath(rootPath);
        _invalidationJournal.Record(fullPath, reason);
        Invalidated?.Invoke(this, fullPath);
    }

    private void QueueWatcherInvalidation(string rootPath, ForgeEvidenceInvalidationReason reason)
    {
        if (reason == ForgeEvidenceInvalidationReason.WatcherOverflow)
        {
            Interlocked.Increment(ref _watcherOverflows);
            Invalidate(rootPath, reason);
            return;
        }

        var replacement = new CancellationTokenSource();
        var previous = _debounceTokens.AddOrUpdate(rootPath, replacement, (_, existing) =>
        {
            existing.Cancel();
            existing.Dispose();
            return replacement;
        });
        if (!ReferenceEquals(previous, replacement))
            previous.Dispose();

        _ = DebounceInvalidationAsync(rootPath, reason, replacement);
    }

    private async Task DebounceInvalidationAsync(
        string rootPath,
        ForgeEvidenceInvalidationReason reason,
        CancellationTokenSource debounceCts)
    {
        try
        {
            await Task.Delay(_options.WatcherDebounce, debounceCts.Token).ConfigureAwait(false);
            Interlocked.Increment(ref _debouncedInvalidations);
            Invalidate(rootPath, reason);
        }
        catch (OperationCanceledException)
        {
            // A newer event for this mod replaced the pending debounce window.
        }
        finally
        {
            if (_debounceTokens.TryGetValue(rootPath, out var current) && ReferenceEquals(current, debounceCts))
                _debounceTokens.TryRemove(rootPath, out _);
            debounceCts.Dispose();
        }
    }

    public void StartWatching(IEnumerable<ModRecord> mods)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StopWatching();
        foreach (var mod in mods.Where(mod => !mod.IsOfficialContent && Directory.Exists(mod.RootPath)))
        {
            try
            {
                var root = Path.GetFullPath(mod.RootPath);
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                   NotifyFilters.LastWrite | NotifyFilters.Size,
                    InternalBufferSize = _options.WatcherBufferSize,
                    EnableRaisingEvents = true
                };
                FileSystemEventHandler changed = (_, args) =>
                {
                    if (ForgeEvidenceWatcherFilter.ShouldInvalidate(args.FullPath, _options.IgnoreTransientFiles))
                        QueueWatcherInvalidation(root, ForgeEvidenceInvalidationReason.FileChanged);
                };
                RenamedEventHandler renamed = (_, args) =>
                {
                    if (ForgeEvidenceWatcherFilter.ShouldInvalidate(args.FullPath, _options.IgnoreTransientFiles) ||
                        ForgeEvidenceWatcherFilter.ShouldInvalidate(args.OldFullPath, _options.IgnoreTransientFiles))
                        QueueWatcherInvalidation(root, ForgeEvidenceInvalidationReason.FileChanged);
                };
                ErrorEventHandler error = (_, _) => QueueWatcherInvalidation(root, ForgeEvidenceInvalidationReason.WatcherOverflow);
                watcher.Changed += changed;
                watcher.Created += changed;
                watcher.Deleted += changed;
                watcher.Renamed += renamed;
                watcher.Error += error;
                lock (_stateGate) _watchers[root] = watcher;
            }
            catch
            {
                Interlocked.Increment(ref _watcherOverflows);
                Invalidate(mod.RootPath, ForgeEvidenceInvalidationReason.WatcherOverflow);
            }
        }
    }

    private int GetWatcherCount()
    {
        lock (_stateGate) return _watchers.Count;
    }

    public void StopWatching()
    {
        List<FileSystemWatcher> watchers;
        lock (_stateGate)
        {
            watchers = _watchers.Values.ToList();
            _watchers.Clear();
        }
        foreach (var watcher in watchers) watcher.Dispose();
    }

    public void CancelCurrent()
    {
        lock (_stateGate) _activeScanCts?.Cancel();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        CancelCurrent();
        StopWatching();
        foreach (var debounce in _debounceTokens.Values)
        {
            debounce.Cancel();
            debounce.Dispose();
        }
        _debounceTokens.Clear();
        _schedulerGate.Dispose();
        return ValueTask.CompletedTask;
    }
}
