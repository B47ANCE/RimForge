using System.Windows;
using System.Windows.Input;

namespace RimForge.App.Commands;

public interface IRimForgeCommandRegistry
{
    void Attach(Window window, RimForgeCommandHandlers handlers);
}

public sealed record RimForgeCommandHandlers(
    Action SelectAll,
    Action Save,
    Action Undo,
    Action Delete,
    Action Rename,
    Action Cancel,
    Func<bool> CanSelectAll,
    Func<bool> CanSave,
    Func<bool> CanUndo,
    Func<bool> CanDelete,
    Func<bool> CanRename,
    Func<bool> CanCancel);

public sealed class RimForgeCommandRegistry : IRimForgeCommandRegistry
{
    public void Attach(Window window, RimForgeCommandHandlers handlers)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(handlers);

        Bind(window, RimForgeCommands.SelectAll, handlers.SelectAll, handlers.CanSelectAll);
        Bind(window, RimForgeCommands.Save, handlers.Save, handlers.CanSave);
        Bind(window, RimForgeCommands.Undo, handlers.Undo, handlers.CanUndo);
        Bind(window, RimForgeCommands.Delete, handlers.Delete, handlers.CanDelete);
        Bind(window, RimForgeCommands.Rename, handlers.Rename, handlers.CanRename);
        Bind(window, RimForgeCommands.Cancel, handlers.Cancel, handlers.CanCancel);

    }

    private static void Bind(Window window, RoutedUICommand command, Action execute, Func<bool> canExecute)
    {
        window.CommandBindings.Add(new CommandBinding(
            command,
            (_, e) => { execute(); e.Handled = true; },
            (_, e) => { e.CanExecute = canExecute(); e.Handled = true; }));

        foreach (var gesture in command.InputGestures.OfType<KeyGesture>())
            window.InputBindings.Add(new KeyBinding(command, gesture));
    }
}
