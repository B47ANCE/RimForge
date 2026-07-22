using System.Windows;
using System.Windows.Controls;

namespace RimForge.App.Features.Console;

public partial class ConsoleView : UserControl
{
    public ConsoleView() => InitializeComponent();
    public ListBox GameLogListControl => GameLogList;

    public void SelectActivityTab() => ConsoleTabs.SelectedItem = ActivityTab;

    public void SelectGameLogTab() => ConsoleTabs.SelectedItem = GameLogTab;

    public void ScrollGameLogIntoView(object item) => GameLogList.ScrollIntoView(item);

    public event RoutedEventHandler? StartWatchingRequested;
    public event RoutedEventHandler? StopWatchingRequested;
    public event RoutedEventHandler? ClearRequested;
    public event ScrollChangedEventHandler? GameLogScrollChanged;

    private void Start_Click(object sender, RoutedEventArgs e) => StartWatchingRequested?.Invoke(this, e);
    private void Stop_Click(object sender, RoutedEventArgs e) => StopWatchingRequested?.Invoke(this, e);
    private void Clear_Click(object sender, RoutedEventArgs e) => ClearRequested?.Invoke(this, e);
    private void GameLogList_ScrollChanged(object sender, ScrollChangedEventArgs e) => GameLogScrollChanged?.Invoke(sender, e);
}
