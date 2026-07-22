using RimForge.Core.BackgroundTasks;
using RimForge.Core.Services;

namespace RimForge.App;

public partial class MainWindow
{
    private void ApplicationStatusService_Changed(object? sender, ApplicationStatusSnapshot snapshot) =>
        Dispatcher.Invoke(() =>
        {
            Notify(nameof(CommandBarStatusText));
            Notify(nameof(CommandBarStatusDetail));
            Notify(nameof(CommandBarStatusBrush));
        });

    private void ProjectManualApplicationStatus(string statusText)
    {
        if (_applicationStatusService is null || BackgroundTask.IsActive) return;
        var normalized = statusText.Trim();
        if (normalized.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("failed", StringComparison.OrdinalIgnoreCase))
            _applicationStatusService.Set(ApplicationStatusKind.Error, "Error", normalized);
        else if (normalized.Contains("scan", StringComparison.OrdinalIgnoreCase))
            _applicationStatusService.Set(ApplicationStatusKind.Scanning, "Scanning", normalized);
        else if (normalized.Contains("forge", StringComparison.OrdinalIgnoreCase))
            _applicationStatusService.Set(ApplicationStatusKind.Forging, "Forging", normalized);
        else if (normalized.Contains("load", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("prepar", StringComparison.OrdinalIgnoreCase))
            _applicationStatusService.Set(ApplicationStatusKind.Loading, "Loading", normalized);
        else
            _applicationStatusService.SetReady(PageTitle);
    }

    private void ProjectBackgroundTaskStatus(BackgroundTaskSnapshot snapshot)
    {
        if (snapshot.State == BackgroundTaskState.Failed)
        {
            _applicationStatusService.Set(ApplicationStatusKind.Error, "Error",
                snapshot.ErrorMessage ?? snapshot.Progress?.Message ?? "Background operation failed.");
            return;
        }

        if (snapshot.State == BackgroundTaskState.Cancelling)
        {
            _applicationStatusService.Set(ApplicationStatusKind.Cancelling, "Cancelling",
                snapshot.Progress?.Message ?? snapshot.DisplayName);
            return;
        }

        if (snapshot.IsActive)
        {
            var kind = snapshot.Key.Contains("scan", StringComparison.OrdinalIgnoreCase)
                ? ApplicationStatusKind.Scanning
                : snapshot.Key.Contains("forge", StringComparison.OrdinalIgnoreCase)
                    ? ApplicationStatusKind.Forging
                    : ApplicationStatusKind.Loading;
            _applicationStatusService.Set(kind, kind.ToString(),
                snapshot.Progress?.Message ?? snapshot.Progress?.Stage ?? snapshot.DisplayName);
            return;
        }

        _applicationStatusService.SetReady(PageTitle);
    }
}
