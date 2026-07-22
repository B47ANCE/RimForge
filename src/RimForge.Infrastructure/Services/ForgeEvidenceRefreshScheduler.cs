using RimForge.Core.Models;

namespace RimForge.Infrastructure.Services;

public sealed record ForgeEvidenceRefreshRequest(
    IReadOnlyList<ModRecord> Mods,
    string RepositoryRoot,
    string TargetRimWorldVersion,
    bool ForceRescan = false);

public sealed record ForgeEvidenceRefreshSchedulerOptions
{
    public static ForgeEvidenceRefreshSchedulerOptions Default { get; } = new();
    public TimeSpan InvalidationSettleDelay { get; init; } = TimeSpan.FromMilliseconds(750);
}

public interface IForgeEvidenceRefreshScheduler : IAsyncDisposable
{
    bool IsEnabled { get; }
    void Configure(ForgeEvidenceRefreshRequest request);
    void Start();
    void Stop();
    Task<ForgeEvidenceSnapshot> RefreshNowAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Converts invalidation notifications into one serialized background refresh using the
/// most recent installed-library request. It never performs analysis on watcher threads.
/// </summary>
public sealed class ForgeEvidenceRefreshScheduler : IForgeEvidenceRefreshScheduler
{
    private readonly IForgeEvidenceService _service;
    private readonly ForgeEvidenceRefreshSchedulerOptions _options;
    private readonly object _gate = new();
    private ForgeEvidenceRefreshRequest? _request;
    private CancellationTokenSource? _settleCts;
    private bool _enabled;
    private bool _disposed;

    public ForgeEvidenceRefreshScheduler(
        IForgeEvidenceService service,
        ForgeEvidenceRefreshSchedulerOptions? options = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _options = options ?? ForgeEvidenceRefreshSchedulerOptions.Default;
        if (_options.InvalidationSettleDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
        _service.Invalidated += OnInvalidated;
    }

    public bool IsEnabled
    {
        get { lock (_gate) return _enabled; }
    }

    public void Configure(ForgeEvidenceRefreshRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RepositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetRimWorldVersion);
        lock (_gate) _request = request with { Mods = request.Mods.ToArray() };
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate) _enabled = true;
    }

    public void Stop()
    {
        CancellationTokenSource? pending;
        lock (_gate)
        {
            _enabled = false;
            pending = _settleCts;
            _settleCts = null;
        }
        pending?.Cancel();
        pending?.Dispose();
    }

    public Task<ForgeEvidenceSnapshot> RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ForgeEvidenceRefreshRequest request;
        lock (_gate)
            request = _request ?? throw new InvalidOperationException("The evidence refresh scheduler has not been configured.");
        return _service.RefreshAsync(
            request.Mods,
            request.RepositoryRoot,
            request.TargetRimWorldVersion,
            cancellationToken: cancellationToken,
            forceRescan: request.ForceRescan);
    }

    private void OnInvalidated(object? sender, string rootPath)
    {
        CancellationTokenSource replacement;
        lock (_gate)
        {
            if (!_enabled || _request is null || _disposed) return;
            _settleCts?.Cancel();
            _settleCts?.Dispose();
            replacement = new CancellationTokenSource();
            _settleCts = replacement;
        }
        _ = RefreshAfterSettleAsync(replacement);
    }

    private async Task RefreshAfterSettleAsync(CancellationTokenSource settleCts)
    {
        try
        {
            await Task.Delay(_options.InvalidationSettleDelay, settleCts.Token).ConfigureAwait(false);
            await RefreshNowAsync(settleCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A newer invalidation or scheduler stop superseded this refresh request.
        }
        catch
        {
            // Producer diagnostics and the next invalidation provide recovery. Watcher callbacks
            // must never surface unobserved exceptions into the process.
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_settleCts, settleCts)) _settleCts = null;
            }
            settleCts.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _service.Invalidated -= OnInvalidated;
        Stop();
        return ValueTask.CompletedTask;
    }
}
