using System.Security.Cryptography;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

/// <summary>
/// Watches RimWorld's active ModsConfig.xml and emits debounced, content-aware
/// changes. RimForge can acknowledge its own successful writes so watcher noise
/// is not misreported as an external edit.
/// </summary>
public sealed class ModsConfigChangeMonitor : IModsConfigChangeMonitor
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _debounce;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCancellation;
    private string? _lastSha256;
    private ExternalProfileChangeKind _pendingKind;
    private int _disposeState;

    public ModsConfigChangeMonitor(TimeSpan? debounce = null)
    {
        _debounce = debounce ?? TimeSpan.FromMilliseconds(350);
    }

    public event EventHandler<ExternalProfileChange>? Changed;

    public string? WatchedPath { get; private set; }

    public async Task StartAsync(string modsConfigPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modsConfigPath);
        ThrowIfDisposed();

        var fullPath = Path.GetFullPath(modsConfigPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("ModsConfig path has no parent directory.", nameof(modsConfigPath));
        var fileName = Path.GetFileName(fullPath);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StopCore();
            Directory.CreateDirectory(directory);
            WatchedPath = fullPath;
            _lastSha256 = await ComputeHashAsync(fullPath, cancellationToken).ConfigureAwait(false);

            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnChanged;
            _watcher.Created += OnCreated;
            _watcher.Deleted += OnDeleted;
            _watcher.Renamed += OnRenamed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AcknowledgeCurrentAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var path = WatchedPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _lastSha256 = await ComputeHashAsync(path, cancellationToken).ConfigureAwait(false);
            _debounceCancellation?.Cancel();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            StopCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => Queue(ExternalProfileChangeKind.Modified);
    private void OnCreated(object sender, FileSystemEventArgs e) => Queue(ExternalProfileChangeKind.Created);
    private void OnDeleted(object sender, FileSystemEventArgs e) => Queue(ExternalProfileChangeKind.Deleted);
    private void OnRenamed(object sender, RenamedEventArgs e) => Queue(ExternalProfileChangeKind.Renamed);

    private void Queue(ExternalProfileChangeKind kind)
    {
        if (Volatile.Read(ref _disposeState) != 0) return;
        _pendingKind = kind;

        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _debounceCancellation, next);
        previous?.Cancel();
        previous?.Dispose();
        _ = PublishAfterDebounceAsync(next.Token);
    }

    private async Task PublishAfterDebounceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_debounce, cancellationToken).ConfigureAwait(false);
            var path = WatchedPath;
            if (string.IsNullOrWhiteSpace(path)) return;

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            ExternalProfileChange? change = null;
            try
            {
                var previous = _lastSha256;
                var current = await ComputeHashAsync(path, cancellationToken).ConfigureAwait(false);
                var exists = File.Exists(path);

                if (!string.Equals(previous, current, StringComparison.OrdinalIgnoreCase) || _pendingKind == ExternalProfileChangeKind.Deleted)
                {
                    _lastSha256 = current;
                    change = new ExternalProfileChange(path, _pendingKind, DateTimeOffset.UtcNow, previous, current, exists);
                }
            }
            finally
            {
                _gate.Release();
            }

            if (change is not null)
                Changed?.Invoke(this, change);
        }
        catch (OperationCanceledException)
        {
            // A newer file-system event superseded this observation.
        }
        catch (IOException)
        {
            // RimWorld may still hold the file briefly; the next watcher event retries.
        }
        catch (UnauthorizedAccessException)
        {
            // Access can be transient during replacement; do not crash the host.
        }
    }

    private static async Task<string?> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, true);
                var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                return Convert.ToHexString(hash);
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(75, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    private void StopCore()
    {
        _debounceCancellation?.Cancel();
        _debounceCancellation?.Dispose();
        _debounceCancellation = null;

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnChanged;
            _watcher.Created -= OnCreated;
            _watcher.Deleted -= OnDeleted;
            _watcher.Renamed -= OnRenamed;
            _watcher.Dispose();
            _watcher = null;
        }

        WatchedPath = null;
        _lastSha256 = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
