using RimForge.Core.BackgroundTasks;

namespace RimForge.Infrastructure.Services;

public sealed class HostedBackgroundWorkService : IHostedBackgroundWorkService
{
    private sealed record WorkRegistration(
        string DisplayName,
        CancellationTokenSource Cancellation,
        Task Completion,
        DateTimeOffset StartedAt);

    private readonly object _gate = new();
    private readonly Dictionary<string, WorkRegistration> _active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HostedBackgroundWorkSnapshot> _snapshots = new(StringComparer.Ordinal);
    private bool _disposed;

    public IReadOnlyList<HostedBackgroundWorkSnapshot> Snapshot
    {
        get
        {
            lock (_gate)
                return _snapshots.Values.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        }
    }

    public event EventHandler<HostedBackgroundWorkSnapshot>? WorkChanged;

    public Task StartAsync(
        string key,
        string displayName,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(operation);

        HostedBackgroundWorkSnapshot starting;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_active.ContainsKey(key)) return Task.CompletedTask;

            var startedAt = DateTimeOffset.UtcNow;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            starting = new(key, displayName, HostedBackgroundWorkState.Starting, startedAt);
            _snapshots[key] = starting;

            // The coordinator is the single ownership boundary for detached hosted work.
            var completion = RunAsync(key, displayName, startedAt, operation, cancellation.Token);
            _active.Add(key, new WorkRegistration(displayName, cancellation, completion, startedAt));
        }

        Publish(starting);
        return Task.CompletedTask;
    }

    public async Task StopAsync(string key, CancellationToken cancellationToken = default)
    {
        WorkRegistration? registration;
        HostedBackgroundWorkSnapshot? stopping = null;
        lock (_gate)
        {
            if (!_active.TryGetValue(key, out registration)) return;
            registration.Cancellation.Cancel();
            stopping = new(key, registration.DisplayName, HostedBackgroundWorkState.Stopping, registration.StartedAt);
            _snapshots[key] = stopping;
        }

        Publish(stopping);
        await registration.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        string[] keys;
        lock (_gate) keys = _active.Keys.ToArray();
        foreach (var key in keys)
            await StopAsync(key, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(
        string key,
        string displayName,
        DateTimeOffset startedAt,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        Publish(Update(key, displayName, HostedBackgroundWorkState.Running, startedAt));
        await Task.Yield();
        try
        {
            await operation(cancellationToken).ConfigureAwait(false);
            Publish(Update(key, displayName, HostedBackgroundWorkState.Stopped, startedAt, DateTimeOffset.UtcNow));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Publish(Update(key, displayName, HostedBackgroundWorkState.Stopped, startedAt, DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            Publish(Update(key, displayName, HostedBackgroundWorkState.Failed, startedAt, DateTimeOffset.UtcNow, ex.Message));
        }
        finally
        {
            lock (_gate)
            {
                if (_active.Remove(key, out var registration)) registration.Cancellation.Dispose();
            }
        }
    }

    private HostedBackgroundWorkSnapshot Update(
        string key,
        string displayName,
        HostedBackgroundWorkState state,
        DateTimeOffset startedAt,
        DateTimeOffset? finishedAt = null,
        string? errorMessage = null)
    {
        var snapshot = new HostedBackgroundWorkSnapshot(key, displayName, state, startedAt, finishedAt, errorMessage);
        lock (_gate) _snapshots[key] = snapshot;
        return snapshot;
    }

    private void Publish(HostedBackgroundWorkSnapshot snapshot) => WorkChanged?.Invoke(this, snapshot);

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        await StopAllAsync().ConfigureAwait(false);
    }
}
