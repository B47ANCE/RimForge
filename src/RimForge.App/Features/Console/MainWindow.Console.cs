using System.Windows;
using System.Windows.Controls;

namespace RimForge.App;

public partial class MainWindow
{
    private void Console_StartWatchingRequested(object sender, RoutedEventArgs e) => StartWatchingGameLog_Click(sender, e);
    private void Console_StopWatchingRequested(object sender, RoutedEventArgs e) => StopWatchingGameLog_Click(sender, e);
    private void Console_ClearRequested(object sender, RoutedEventArgs e) => ClearGameLog_Click(sender, e);
    private void Console_GameLogScrollChanged(object sender, ScrollChangedEventArgs e) => GameLogList_ScrollChanged(sender, e);
}
