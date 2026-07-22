using System.Diagnostics;
using RimForge.Core.BackgroundTasks;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class BackgroundTaskService : IBackgroundTaskService
{
    private readonly IApplicationEventBus? _eventBus;
    private readonly object _gate = new();
    private BackgroundTaskSnapshot _current = BackgroundTaskSnapshot.Idle;
    private CancellationTokenSource? _currentCancellation;
    private Stopwatch? _stopwatch;

    public BackgroundTaskService(IApplicationEventBus? eventBus = null) => _eventBus = eventBus;

    public BackgroundTaskSnapshot Current
    {
        get
        {
            lock (_gate)
                return WithElapsed(_current);
        }
    }

    public bool IsRunning => Current.IsActive;

    public event EventHandler<BackgroundTaskSnapshot>? TaskChanged;

    public async Task<T> RunAsync<T>(
        string key,
        string displayName,
        Func<BackgroundTaskContext, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(operation);

        CancellationTokenSource linkedCancellation;
        lock (_gate)
        {
            if (_current.IsActive)
                throw new InvalidOperationException($"Background task '{_current.DisplayName}' is already running.");

            _currentCancellation?.Dispose();
            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _currentCancellation = linkedCancellation;
            _stopwatch = Stopwatch.StartNew();
            _current = new BackgroundTaskSnapshot(
                key,
                displayName,
                BackgroundTaskState.Running,
                new BackgroundTaskProgress("Starting", $"Starting {displayName}.", string.Empty),
                DateTimeOffset.Now,
                null,
                TimeSpan.Zero);
        }
        Publish();

        var context = new BackgroundTaskContext(linkedCancellation.Token, Report);
        try
        {
            // This is the single worker-scheduling boundary for RimForge feature work.
            // Feature implementations provide cancellable operations and never create
            // isolated Task.Run pipelines of their own.
            var result = await Task.Run(() => operation(context), linkedCancellation.Token).ConfigureAwait(false);
            linkedCancellation.Token.ThrowIfCancellationRequested();
            Complete(BackgroundTaskState.Completed, null);
            return result;
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            Complete(BackgroundTaskState.Cancelled, null);
            throw;
        }
        catch (Exception ex)
        {
            Complete(BackgroundTaskState.Failed, ex.Message);
            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_currentCancellation, linkedCancellation))
                    _currentCancellation = null;
            }
            linkedCancellation.Dispose();
        }
    }

    public Task RunAsync(
        string key,
        string displayName,
        Func<BackgroundTaskContext, Task> operation,
        CancellationToken cancellationToken = default) =>
        RunAsync<object?>(
            key,
            displayName,
            async context =>
            {
                await operation(context).ConfigureAwait(false);
                return null;
            },
            cancellationToken);

    public bool CancelCurrent(string message = "Cancellation requested.")
    {
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            if (!_current.IsActive || _currentCancellation is null)
                return false;

            _current = WithElapsed(_current) with
            {
                State = BackgroundTaskState.Cancelling,
                Progress = new BackgroundTaskProgress(
                    "Cancelling",
                    message,
                    _current.Progress?.TechnicalDetail ?? string.Empty,
                    _current.Progress?.Percent,
                    _current.Progress?.Completed ?? 0,
                    _current.Progress?.Total ?? 0,
                    _current.Progress?.DiscoveryDetail ?? string.Empty,
                    _current.Progress?.CurrentFile ?? string.Empty)
            };
            cancellation = _currentCancellation;
        }

        Publish();
        cancellation.Cancel();
        return true;
    }

    public void Reset()
    {
        lock (_gate)
        {
            if (_current.IsActive)
                throw new InvalidOperationException("An active background task cannot be reset.");
            _current = BackgroundTaskSnapshot.Idle;
            _stopwatch = null;
        }
        Publish();
    }

    public void Report(BackgroundTaskProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        lock (_gate)
        {
            if (_current.State != BackgroundTaskState.Running)
                return;
            _current = WithElapsed(_current) with { Progress = Normalize(progress) };
        }
        Publish();
    }

    private void Complete(BackgroundTaskState state, string? errorMessage)
    {
        lock (_gate)
        {
            _stopwatch?.Stop();
            var previous = _current.Progress;
            var stage = state switch
            {
                BackgroundTaskState.Completed => "Completed",
                BackgroundTaskState.Cancelled => "Cancelled",
                BackgroundTaskState.Failed => "Failed",
                _ => previous?.Stage ?? string.Empty
            };
            var message = state switch
            {
                BackgroundTaskState.Completed => $"{_current.DisplayName} completed.",
                BackgroundTaskState.Cancelled => $"{_current.DisplayName} cancelled.",
                BackgroundTaskState.Failed => errorMessage ?? $"{_current.DisplayName} failed.",
                _ => previous?.Message ?? string.Empty
            };
            _current = WithElapsed(_current) with
            {
                State = state,
                FinishedAt = DateTimeOffset.Now,
                ErrorMessage = errorMessage,
                Progress = new BackgroundTaskProgress(
                    stage,
                    message,
                    previous?.TechnicalDetail ?? string.Empty,
                    state == BackgroundTaskState.Completed ? 100d : previous?.EffectivePercent,
                    state == BackgroundTaskState.Completed && previous is { Total: > 0 }
                        ? previous.Total
                        : previous?.Completed ?? 0,
                    previous?.Total ?? 0,
                    previous?.DiscoveryDetail ?? string.Empty,
                    previous?.CurrentFile ?? string.Empty)
            };
        }
        Publish();
    }

    private static BackgroundTaskProgress Normalize(BackgroundTaskProgress progress) => progress with
    {
        Percent = progress.EffectivePercent,
        Completed = Math.Max(0, progress.Completed),
        Total = Math.Max(0, progress.Total)
    };

    private BackgroundTaskSnapshot WithElapsed(BackgroundTaskSnapshot snapshot) =>
        snapshot with { Elapsed = _stopwatch?.Elapsed ?? snapshot.Elapsed };

    private void Publish()
    {
        var snapshot = Current;
        TaskChanged?.Invoke(this, snapshot);
        _eventBus?.Publish(new BackgroundTaskChangedEvent(snapshot));
    }
}
