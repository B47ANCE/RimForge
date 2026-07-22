using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.App;

public partial class MainWindow
{
    private async Task EnsureModsConfigMonitorAsync()
    {
        var path = _profileWorkspaceService.GetRimWorldModsConfigPath();
        if (string.Equals(_modsConfigChangeMonitor.WatchedPath, path, StringComparison.OrdinalIgnoreCase)) return;
        await _modsConfigChangeMonitor.StartAsync(path);
        Append($"Watching active RimWorld profile: {path}", ActivitySeverity.Info);
    }

    private void ModsConfigChangeMonitor_Changed(object? sender, ExternalProfileChange change) =>
        Dispatcher.BeginInvoke(new Action(async () => await ReconcileExternalProfileChangeAsync(change)));

    private async Task ReconcileExternalProfileChangeAsync(ExternalProfileChange change)
    {
        if (SelectedProfile is null || !change.FileExists) return;
        try
        {
            var external = await _externalProfileReconciliationService.ReadAsync(change.Path);
            var reconciliation = _externalProfileReconciliationService.Compare(SelectedProfile, external);
            if (reconciliation.IsIdentical) return;
            _pendingExternalProfileReconciliation = reconciliation;
            Append($"External ModsConfig.xml change detected for {SelectedProfile.Name}: {reconciliation.Summary}", ActivitySeverity.Warning);
            _notificationService.Enqueue(new NotificationRequest(
                "RimWorld profile changed externally",
                reconciliation.Summary,
                NotificationSeverity.Warning,
                [new NotificationAction("accept-external-profile", "Use External"),
                 new NotificationAction("restore-rimforge-profile", "Restore RimForge"),
                 new NotificationAction("defer-external-profile", "Decide Later"),
                 new NotificationAction("view-activity", "View Details")],
                Duration: TimeSpan.FromSeconds(20)));
        }
        catch (Exception ex)
        {
            Append("External profile reconciliation failed: " + ex.Message, ActivitySeverity.Error);
        }
    }

    private async Task AcceptExternalProfileAsync()
    {
        var pending = _pendingExternalProfileReconciliation;
        if (pending is null) return;
        var result = await _externalProfileConflictService.ResolveAsync(pending, ExternalProfileResolution.AdoptExternal);
        if (result.Success)
        {
            if (result.RequiresAcknowledgement) await _modsConfigChangeMonitor.AcknowledgeCurrentAsync();
            await LoadProfilesAsync();
            _pendingExternalProfileReconciliation = null;
        }
        _notificationService.Enqueue(new NotificationRequest("External profile import", result.Message,
            result.Success ? NotificationSeverity.Success : NotificationSeverity.Error));
    }

    private async Task RestoreRimForgeProfileAsync()
    {
        var pending = _pendingExternalProfileReconciliation;
        if (pending is null) return;
        var result = await _externalProfileConflictService.ResolveAsync(pending, ExternalProfileResolution.RestoreRimForge);
        if (result.Success)
        {
            if (result.RequiresAcknowledgement) await _modsConfigChangeMonitor.AcknowledgeCurrentAsync();
            _pendingExternalProfileReconciliation = null;
        }
        _notificationService.Enqueue(new NotificationRequest("RimForge profile restore", result.Message,
            result.Success ? NotificationSeverity.Success : NotificationSeverity.Error));
    }

    private async Task DeferExternalProfileAsync()
    {
        var pending = _pendingExternalProfileReconciliation;
        if (pending is null) return;
        var result = await _externalProfileConflictService.ResolveAsync(pending, ExternalProfileResolution.Defer);
        _pendingExternalProfileReconciliation = null;
        Append(result.Message, ActivitySeverity.Info);
        _notificationService.Enqueue(new NotificationRequest(
            "External profile reconciliation deferred",
            result.Message,
            NotificationSeverity.Information));
    }
}
