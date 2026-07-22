using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RimForge.App.Commands;

namespace RimForge.App;

public partial class MainWindow
{
    private void ConfigureCommandFramework()
    {
        _commandRegistry.Attach(this, new RimForgeCommandHandlers(
            SelectAllInFocusedModList,
            () => SaveLoadOrder_Click(this, new RoutedEventArgs()),
            ExecuteUndoCommand,
            () => DeleteProfile_Click(this, new RoutedEventArgs()),
            () => RenameProfile_Click(this, new RoutedEventArgs()),
            ExecuteCancelCommand,
            CanSelectAllInFocusedModList,
            () => SelectedProfile is not null && IsLoadOrderDirty,
            () => _undoService.CanUndo,
            () => CanDeleteSelectedProfile,
            () => CanRenameSelectedProfile,
            () => _backgroundTaskService.IsRunning || !string.IsNullOrWhiteSpace(SearchText)));
    }

    private ListBox? GetFocusedModList()
    {
        if (ModSorterFeature.ActiveList.IsKeyboardFocusWithin)
            return ModSorterFeature.ActiveList;
        if (ModSorterFeature.InactiveList.IsKeyboardFocusWithin)
            return ModSorterFeature.InactiveList;
        return null;
    }

    private bool CanSelectAllInFocusedModList() => GetFocusedModList()?.Items.Count > 0;

    private void SelectAllInFocusedModList()
    {
        var list = GetFocusedModList();
        if (list is null) return;
        list.SelectAll();
    }

    private void ExecuteUndoCommand()
    {
        if (_undoService.TryUndo())
            Append("Undid the most recent unsaved Mod Sorter action.", RimForge.Core.Models.ActivitySeverity.Info);
    }

    private void UndoService_StateChanged(object? sender, EventArgs e)
    {
        Notify(nameof(CanUndo));
        Notify(nameof(UndoPreviewText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void CommandBar_UndoRequested(object sender, RoutedEventArgs e) => ExecuteUndoCommand();

    private void ExecuteCancelCommand()
    {
        if (_backgroundTaskService.IsRunning)
        {
            Cancel_Click(this, new RoutedEventArgs());
            return;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
            SearchText = string.Empty;
    }
}
