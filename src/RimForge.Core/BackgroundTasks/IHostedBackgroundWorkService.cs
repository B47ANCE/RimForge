namespace RimForge.Core.BackgroundTasks;

public interface IHostedBackgroundWorkService : IAsyncDisposable
{
    IReadOnlyList<HostedBackgroundWorkSnapshot> Snapshot { get; }
    event EventHandler<HostedBackgroundWorkSnapshot>? WorkChanged;

    Task StartAsync(
        string key,
        string displayName,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task StopAsync(string key, CancellationToken cancellationToken = default);
    Task StopAllAsync(CancellationToken cancellationToken = default);
}
