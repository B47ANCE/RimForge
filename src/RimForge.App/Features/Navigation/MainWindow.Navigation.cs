using System.Windows;

namespace RimForge.App;

public partial class MainWindow
{
    private string _activeWorkspaceDestination = "Mod Sorter";

    private void CommandBar_NavigationRequested(object? sender, string destination) =>
        NavigateToWorkspaceDestination(destination);

    private void NavigateToWorkspaceDestination(string destination)
    {
        FrameworkElement section = destination switch
        {
            "Issue Viewer" => IssueViewerWorkspacePanel,
            "ForgeView" => ForgeViewPanel,
            "Texture Tools" => TextureToolsPanel,
            "Settings" => SettingsPanel,
            "Console" => ConsolePanel,
            _ => DashboardPanel
        };

        var parent = destination == "Issue Viewer" ? "Mod Sorter" : destination;
        ScrollToWorkspaceSection(section, parent);
        UpdateNavigationLocation(destination);
    }

    private void UpdateNavigationState(string pageTitle) =>
        UpdateNavigationLocation(pageTitle);

    private void UpdateNavigationLocation(string destination)
    {
        EngineeringCommandBar?.SetLocation(destination);
        if (string.Equals(_activeWorkspaceDestination, destination, StringComparison.OrdinalIgnoreCase)) return;
        _activeWorkspaceDestination = destination;
        Notify(nameof(SearchPromptText));
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => ScrollToWorkspaceSection(SettingsPanel, "Settings");

    private void OpenProfileManagement_Click(object sender, RoutedEventArgs e)
    {
        ScrollToWorkspaceSection(SettingsPanel, "Settings");
        SettingsFeature.SelectProfilesTab();
        UpdateNavigationLocation("Settings");
    }
}
