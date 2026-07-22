using RimForge.Core.BackgroundTasks;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.App;

public partial class MainWindow
{
    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public SynchronousProgress(Action<T> report) => _report = report;

        public void Report(T value) => _report(value);
    }

    internal Task RunFeatureTaskAsync(
        string key,
        string displayName,
        Func<BackgroundTaskContext, Task> operation,
        CancellationToken cancellationToken = default) =>
        RunFeatureTaskAsync<object?>(
            key,
            displayName,
            async context =>
            {
                await operation(context).ConfigureAwait(false);
                return null;
            },
            cancellationToken);

    internal async Task<T> RunFeatureTaskAsync<T>(
        string key,
        string displayName,
        Func<BackgroundTaskContext, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(operation);

        if (_backgroundTaskService.Current.IsActive &&
            _backgroundTaskService.Current.Key.Equals("intelligence.refresh", StringComparison.OrdinalIgnoreCase) &&
            !key.Equals("intelligence.refresh", StringComparison.OrdinalIgnoreCase))
        {
            CancelFeatureTask($"Pausing shared intelligence so {displayName} can run.");
            var intelligenceTask = _backgroundIntelligenceTask;
            if (intelligenceTask is not null)
            {
                try
                {
                    await intelligenceTask;
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is the expected handoff into the requested operation.
                }
            }
            while (_backgroundTaskService.Current.IsActive)
                await Task.Delay(25, cancellationToken);
        }

        Append($"{displayName} started.", ActivitySeverity.Info);
        try
        {
            var result = await _backgroundTaskService
                .RunAsync(key, displayName, operation, cancellationToken);
            Append($"{displayName} completed in {_backgroundTaskService.Current.Elapsed.TotalSeconds:0.0} seconds.", ActivitySeverity.Success);
            return result;
        }
        catch (OperationCanceledException)
        {
            Append($"{displayName} cancelled after {_backgroundTaskService.Current.Elapsed.TotalSeconds:0.0} seconds.", ActivitySeverity.Warning);
            throw;
        }
        catch (Exception ex)
        {
            Append($"{displayName} failed: {ex.Message}", ActivitySeverity.Error);
            _notificationService.Enqueue(new NotificationRequest(
                $"{displayName} failed",
                ex.Message,
                NotificationSeverity.Error,
                [new NotificationAction("view-activity", "View Log")],
                TimeSpan.FromSeconds(15)));
            throw;
        }
    }

    internal bool CancelFeatureTask(string message = "Cancellation requested.")
    {
        var activeTask = _backgroundTaskService.Current;
        var displayName = activeTask.DisplayName;
        if (!_backgroundTaskService.CancelCurrent(message))
            return false;

        if (activeTask.Key.Equals("intelligence.refresh", StringComparison.OrdinalIgnoreCase) ||
            activeTask.Key.Equals("forge.evidence", StringComparison.OrdinalIgnoreCase))
        {
            _forgeEvidenceService.CancelCurrent();
        }

        Append(
            string.IsNullOrWhiteSpace(displayName)
                ? message
                : $"Cancellation requested for {displayName}.",
            ActivitySeverity.Warning);
        return true;
    }

    internal bool IsFeatureTaskRunning(string key) =>
        _backgroundTaskService.Current.IsActive &&
        _backgroundTaskService.Current.Key.Equals(key, StringComparison.OrdinalIgnoreCase);

    private async Task ExecuteFeatureCommandAsync(string displayName, Func<Task> command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            await command();
        }
        catch (OperationCanceledException)
        {
            // RunFeatureTaskAsync records the canonical cancellation transition and log entry.
        }
        catch (Exception ex)
        {
            // Background-task failures have already been logged and notified by the shared
            // runner. This fallback also protects UI-only command continuations.
            var snapshot = _backgroundTaskService.Current;
            var alreadyReported = snapshot.State == BackgroundTaskState.Failed &&
                string.Equals(snapshot.ErrorMessage, ex.Message, StringComparison.Ordinal);
            if (!alreadyReported)
            {
                Append($"{displayName} failed: {ex.Message}", ActivitySeverity.Error);
                _notificationService.Enqueue(new NotificationRequest(
                    $"{displayName} failed",
                    ex.Message,
                    NotificationSeverity.Error,
                    [new NotificationAction("view-activity", "View Log")],
                    TimeSpan.FromSeconds(15)));
            }
        }
    }

    private Task<ProfileOperationResult> RunProfileOperationAsync(
        string key,
        string displayName,
        string technicalDetail,
        string currentFile,
        Func<CancellationToken, Task<ProfileOperationResult>> operation) =>
        RunFeatureTaskAsync(
            key,
            displayName,
            context =>
            {
                context.Report(new BackgroundTaskProgress(
                    displayName,
                    technicalDetail,
                    technicalDetail,
                    null,
                    0,
                    1,
                    "Updating profile workspace",
                    currentFile));
                return operation(context.CancellationToken);
            });

    public string BackgroundTaskCurrentOperation =>
        BackgroundTask.Progress?.Message ?? BackgroundTask.DisplayName;

    public string BackgroundTaskDiscoveryDetail =>
        BackgroundTask.Progress?.DiscoveryDetail ?? string.Empty;

    public string BackgroundTaskCurrentFile =>
        BackgroundTask.Progress?.CurrentFile ?? string.Empty;

    public string BackgroundTaskCountText => BackgroundTask.Progress is { Total: > 0 } progress
        ? $"{Math.Min(progress.Completed, progress.Total)} / {progress.Total}"
        : string.Empty;

    public double BackgroundTaskProgressValue =>
        BackgroundTask.Progress?.EffectivePercent ?? 0d;

    public bool IsBackgroundTaskProgressIndeterminate =>
        BackgroundTask.IsActive && BackgroundTask.Progress?.IsIndeterminate != false;

    public bool IsGeneralFeatureTaskVisible => BackgroundTask.IsActive &&
        !BackgroundTask.Key.Equals("library.scan", StringComparison.OrdinalIgnoreCase) &&
        !BackgroundTask.Key.StartsWith("forge.", StringComparison.OrdinalIgnoreCase);

    private void NotifyBackgroundTaskProjection()
    {
        Notify(nameof(IsBackgroundTaskRunning));
        Notify(nameof(BackgroundTaskElapsedText));
        Notify(nameof(BackgroundTaskCurrentOperation));
        Notify(nameof(BackgroundTaskTechnicalDetail));
        Notify(nameof(BackgroundTaskDiscoveryDetail));
        Notify(nameof(BackgroundTaskCurrentFile));
        Notify(nameof(BackgroundTaskCountText));
        Notify(nameof(BackgroundTaskProgressValue));
        Notify(nameof(IsBackgroundTaskProgressIndeterminate));
        Notify(nameof(IsGeneralFeatureTaskVisible));
        Notify(nameof(CommandBarStatusText));
        Notify(nameof(CommandBarStatusDetail));
        Notify(nameof(CommandBarStatusBrush));
    }
}
