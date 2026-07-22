namespace RimForge.Core.BackgroundTasks;

public interface IBackgroundTaskService
{
    BackgroundTaskSnapshot Current { get; }
    bool IsRunning { get; }
    event EventHandler<BackgroundTaskSnapshot>? TaskChanged;

    Task<T> RunAsync<T>(
        string key,
        string displayName,
        Func<BackgroundTaskContext, Task<T>> operation,
        CancellationToken cancellationToken = default);

    Task RunAsync(
        string key,
        string displayName,
        Func<BackgroundTaskContext, Task> operation,
        CancellationToken cancellationToken = default);

    void Report(BackgroundTaskProgress progress);
    bool CancelCurrent(string message = "Cancellation requested.");
    void Reset();
}
