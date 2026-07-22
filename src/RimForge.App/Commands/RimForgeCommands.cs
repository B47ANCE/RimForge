using System.Windows.Input;

namespace RimForge.App.Commands;

public static class RimForgeCommands
{
    public static readonly RoutedUICommand SelectAll = Create("Select all", nameof(SelectAll), Key.A, ModifierKeys.Control);
    public static readonly RoutedUICommand Save = Create("Save", nameof(Save), Key.S, ModifierKeys.Control);
    public static readonly RoutedUICommand Undo = Create("Undo", nameof(Undo), Key.Z, ModifierKeys.Control);
    public static readonly RoutedUICommand Delete = Create("Delete", nameof(Delete), Key.Delete, ModifierKeys.None);
    public static readonly RoutedUICommand Rename = Create("Rename", nameof(Rename), Key.F2, ModifierKeys.None);
    public static readonly RoutedUICommand Cancel = Create("Cancel", nameof(Cancel), Key.Escape, ModifierKeys.None);

    private static RoutedUICommand Create(string text, string name, Key key, ModifierKeys modifiers) =>
        new(text, name, typeof(RimForgeCommands), new InputGestureCollection { new KeyGesture(key, modifiers) });
}
