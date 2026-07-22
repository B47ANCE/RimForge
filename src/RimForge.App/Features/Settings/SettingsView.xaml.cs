using System.Windows;
using System.Windows.Controls;

namespace RimForge.App.Features.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    public event RoutedEventHandler? SearchSteamLibrariesRequested;
    public event RoutedEventHandler? SaveRequested;
    public event RoutedEventHandler? ProfileCreateRequested;
    public event RoutedEventHandler? ProfileRenameRequested;
    public event RoutedEventHandler? ProfileOpenRequested;
    public event RoutedEventHandler? ProfileDuplicateRequested;
    public event RoutedEventHandler? ProfileFavoriteRequested;
    public event RoutedEventHandler? ProfileImportRequested;
    public event RoutedEventHandler? ProfileExportRequested;
    public event RoutedEventHandler? ProfileCompareRequested;
    public event RoutedEventHandler? ProfileLockRequested;
    public event RoutedEventHandler? ProfileDeleteRequested;

    public void SelectProfilesTab() => SettingsTabs.SelectedItem = ProfilesTab;

    private void SearchSteamLibraries_Click(object sender, RoutedEventArgs e) =>
        SearchSteamLibrariesRequested?.Invoke(this, e);

    private void SaveSettings_Click(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, e);
    private void ProfileCreate_Click(object sender, RoutedEventArgs e) => ProfileCreateRequested?.Invoke(this, e);
    private void ProfileRename_Click(object sender, RoutedEventArgs e) => ProfileRenameRequested?.Invoke(this, e);
    private void ProfileOpen_Click(object sender, RoutedEventArgs e) => ProfileOpenRequested?.Invoke(this, e);
    private void ProfileDuplicate_Click(object sender, RoutedEventArgs e) => ProfileDuplicateRequested?.Invoke(this, e);
    private void ProfileFavorite_Click(object sender, RoutedEventArgs e) => ProfileFavoriteRequested?.Invoke(this, e);
    private void ProfileImport_Click(object sender, RoutedEventArgs e) => ProfileImportRequested?.Invoke(this, e);
    private void ProfileExport_Click(object sender, RoutedEventArgs e) => ProfileExportRequested?.Invoke(this, e);
    private void ProfileCompare_Click(object sender, RoutedEventArgs e) => ProfileCompareRequested?.Invoke(this, e);
    private void ProfileLock_Click(object sender, RoutedEventArgs e) => ProfileLockRequested?.Invoke(this, e);
    private void ProfileDelete_Click(object sender, RoutedEventArgs e) => ProfileDeleteRequested?.Invoke(this, e);
}
