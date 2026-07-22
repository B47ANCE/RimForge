using System.Windows;
using System.Windows.Input;
using RimForge.Core.Services;
using RimForge.UI.Dialogs;

namespace RimForge.App;

public partial class MainWindow
{
    private bool _isRestoringGlobalNavigation;
    private bool _isReforging;

    public bool CanNavigateBack => _globalNavigationService?.CanGoBack == true;
    public bool CanNavigateForward => _globalNavigationService?.CanGoForward == true;
    public bool CanReforge => !_isReforging && !BackgroundTask.IsActive;
    public string ReforgeToolTip => IsLoadOrderDirty
        ? "Reforge the workstation. Unsaved load-order work cannot be reconstructed and requires confirmation."
        : "Soft-refresh RimForge while preserving page, profile, search, and selection.";

    private WorkstationNavigationSnapshot CaptureGlobalNavigationSnapshot() => new(
        PageTitle,
        SelectedMod?.PackageId,
        SearchText,
        SelectedProfile?.Name);

    private void RecordGlobalNavigationSnapshot()
    {
        if (_isRestoringGlobalNavigation || _globalNavigationService is null) return;
        _globalNavigationService.Record(CaptureGlobalNavigationSnapshot());
    }

    private void GlobalNavigationService_StateChanged(object? sender, EventArgs e)
    {
        Notify(nameof(CanNavigateBack));
        Notify(nameof(CanNavigateForward));
        CommandManager.InvalidateRequerySuggested();
    }

    private void CommandBar_BackRequested(object sender, RoutedEventArgs e) => NavigateGlobalHistory(-1);
    private void CommandBar_ForwardRequested(object sender, RoutedEventArgs e) => NavigateGlobalHistory(1);
    private async void CommandBar_ReforgeRequested(object sender, RoutedEventArgs e) => await ExecuteReforgeAsync();

    private void NavigateGlobalHistory(int offset)
    {
        var snapshot = offset < 0
            ? _globalNavigationService.GoBack()
            : _globalNavigationService.GoForward();
        if (snapshot is null) return;
        RestoreGlobalNavigationSnapshot(snapshot);
    }

    private void RestoreGlobalNavigationSnapshot(WorkstationNavigationSnapshot snapshot)
    {
        _isRestoringGlobalNavigation = true;
        try
        {
            SearchText = snapshot.SearchText;
            if (!string.IsNullOrWhiteSpace(snapshot.SelectedProfileName))
                SelectedProfile = Profiles.FirstOrDefault(profile =>
                    string.Equals(profile.Name, snapshot.SelectedProfileName, StringComparison.OrdinalIgnoreCase))
                    ?? SelectedProfile;
            if (!string.IsNullOrWhiteSpace(snapshot.SelectedPackageId))
                SelectModByPackageId(snapshot.SelectedPackageId);
            var section = GetWorkspaceSections().FirstOrDefault(item =>
                string.Equals(item.Title, snapshot.PageTitle, StringComparison.OrdinalIgnoreCase));
            if (section.Element is not null)
                ScrollToWorkspaceSection(section.Element, section.Title, recordHistory: false);
        }
        finally
        {
            _isRestoringGlobalNavigation = false;
        }
    }

    private async Task ExecuteReforgeAsync()
    {
        if (!CanReforge) return;
        if (IsLoadOrderDirty && !ForgeDialogService.ShowConfirmation(
                this,
                "Reforge RimForge",
                "The current load-order workspace contains unsaved changes. Those pending edits cannot be reconstructed after the library is refreshed. Save them first, or continue and discard only those unsaved edits.",
                "Reforge Anyway"))
            return;

        var snapshot = CaptureGlobalNavigationSnapshot();
        _isReforging = true;
        Notify(nameof(CanReforge));
        StatusText = "Reforging";
        StatusBrush = (System.Windows.Media.Brush)FindResource("WarningBrush");
        try
        {
            await ScanNativeLibraryAsync();
            RestoreGlobalNavigationSnapshot(snapshot);
            StatusText = "Ready";
            StatusBrush = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            Append("Reforge completed. Workspace context was restored where available.", RimForge.Core.Models.ActivitySeverity.Success);
        }
        catch (Exception ex)
        {
            StatusText = "Error";
            StatusBrush = (System.Windows.Media.Brush)FindResource("DangerBrush");
            Append($"Reforge failed: {ex.Message}", RimForge.Core.Models.ActivitySeverity.Error);
        }
        finally
        {
            _isReforging = false;
            Notify(nameof(CanReforge));
            Notify(nameof(ReforgeToolTip));
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        if (e.Key == Key.F)
        {
            EngineeringCommandBar.FocusGlobalSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Left && CanNavigateBack) { NavigateGlobalHistory(-1); e.Handled = true; }
        else if (e.Key == Key.Right && CanNavigateForward) { NavigateGlobalHistory(1); e.Handled = true; }
        else if (e.Key == Key.Up && CanNavigateSelectionBack) { NavigateActiveCollection(-1); e.Handled = true; }
        else if (e.Key == Key.Down && CanNavigateSelectionForward) { NavigateActiveCollection(1); e.Handled = true; }
    }
}
