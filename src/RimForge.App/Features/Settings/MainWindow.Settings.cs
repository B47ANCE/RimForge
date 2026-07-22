using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RimForge.App.Serialization;
using RimForge.Core.BackgroundTasks;
using RimForge.Core.Diagnostics;
using RimForge.Core.Models;
using RimForge.Core.Services;
using RimForge.Infrastructure.Services;

namespace RimForge.App;

public partial class MainWindow
{
    private sealed record SortingPolicySettings(
        LoadOrderSortingMode Mode,
        bool LaterBandsOverrideEarlier,
        bool ReconcileDependenciesDownwardOnly,
        bool CuratedAnchorsBidirectional,
        string RulePackId);

    private SortingPolicySettings _sortingPolicySettings = new(
        LoadOrderSortingMode.CategoryFirst,
        LaterBandsOverrideEarlier: true,
        ReconcileDependenciesDownwardOnly: true,
        CuratedAnchorsBidirectional: true,
        RulePackId: "rimforge.category-first.v1");

    // Settings-owned bindable state lives with the Settings feature boundary.
    public IReadOnlyList<DependencyAssistanceMode> DependencyAssistanceModes { get; } = Enum.GetValues<DependencyAssistanceMode>();
    public DependencyAssistanceMode DependencyAssistancePreference { get => _dependencyAssistanceMode; set => Set(ref _dependencyAssistanceMode, value); }
    public IReadOnlyList<OrphanCleanupMode> OrphanCleanupModes { get; } = Enum.GetValues<OrphanCleanupMode>();
    public OrphanCleanupMode OrphanCleanupPreference { get => _orphanCleanupMode; set => Set(ref _orphanCleanupMode, value); }
    public string WorkshopFolder { get => _workshopFolder; set => Set(ref _workshopFolder, value); }
    public string LocalModsFolder { get => _localModsFolder; set => Set(ref _localModsFolder, value); }
    public string OutputFolderSetting { get => _outputFolderSetting; set => Set(ref _outputFolderSetting, value); }
    public int ExternalTimeoutSeconds { get => _externalTimeoutSeconds; set => Set(ref _externalTimeoutSeconds, value); }
    public bool ShowForgeNarrative { get => _showForgeNarrative; set => Set(ref _showForgeNarrative, value); }
    public bool OpenConsoleOnGameLaunch { get => _openConsoleOnGameLaunch; set => Set(ref _openConsoleOnGameLaunch, value); }
    public IReadOnlyList<string> RimWorldVersions { get; } = new[] { "1.0", "1.1", "1.2", "1.3", "1.4", "1.5", "1.6" };
    public string TargetRimWorldVersion { get => _targetRimWorldVersion; set => Set(ref _targetRimWorldVersion, value); }
    public string SettingsStatus { get => _settingsStatus; set => Set(ref _settingsStatus, value); }
    public IReadOnlyList<LoadOrderSortingMode> LoadOrderSortingModes { get; } = Enum.GetValues<LoadOrderSortingMode>();
    public LoadOrderSortingMode LoadOrderSortingPreference
    {
        get => _sortingPolicySettings.Mode;
        set { if (_sortingPolicySettings.Mode == value) return; _sortingPolicySettings = _sortingPolicySettings with { Mode = value }; Notify(nameof(LoadOrderSortingPreference)); }
    }
    public bool LaterBandsOverrideEarlier { get => _sortingPolicySettings.LaterBandsOverrideEarlier; set { _sortingPolicySettings = _sortingPolicySettings with { LaterBandsOverrideEarlier = value }; Notify(nameof(LaterBandsOverrideEarlier)); } }
    public bool ReconcileDependenciesDownwardOnly { get => _sortingPolicySettings.ReconcileDependenciesDownwardOnly; set { _sortingPolicySettings = _sortingPolicySettings with { ReconcileDependenciesDownwardOnly = value }; Notify(nameof(ReconcileDependenciesDownwardOnly)); } }
    public bool CuratedAnchorsBidirectional { get => _sortingPolicySettings.CuratedAnchorsBidirectional; set { _sortingPolicySettings = _sortingPolicySettings with { CuratedAnchorsBidirectional = value }; Notify(nameof(CuratedAnchorsBidirectional)); } }

    private bool ConfiguredModPathsExist() =>
        Directory.Exists(Environment.ExpandEnvironmentVariables(WorkshopFolder)) &&
        Directory.Exists(Environment.ExpandEnvironmentVariables(LocalModsFolder));

    private async Task EnsureValidModPathsAsync()
    {
        if (ConfiguredModPathsExist()) return;
        await DiscoverSteamLibrariesAsync(saveWhenFound: true);
    }

    private async void Settings_SearchSteamLibrariesRequested(object sender, RoutedEventArgs e)
    {
        await ExecuteFeatureCommandAsync(
            "Discover Steam Libraries",
            () => DiscoverSteamLibrariesAsync(saveWhenFound: true));
    }

    private async Task DiscoverSteamLibrariesAsync(bool saveWhenFound)
    {
        var candidates = await RunFeatureTaskAsync(
            "settings.discover-steam",
            "Discover Steam Libraries",
            context =>
            {
                context.Report(new BackgroundTaskProgress(
                    "Discovering Steam libraries",
                    "Searching configured drives for a RimWorld installation.",
                    "libraryfolders.vdf",
                    null,
                    0,
                    0,
                    "Checking Steam library metadata",
                    string.Empty));
                context.CancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(_steamLibraryDiscoveryService.FindRimWorldInstallations());
            });
        if (candidates.Count == 0)
        {
            SettingsStatus = "No RimWorld Steam library was found. Choose the folders manually.";
            Append(SettingsStatus, ActivitySeverity.Warning);
            return;
        }

        var candidate = candidates.Count == 1 ? candidates[0] : ChooseSteamInstallation(candidates);
        if (candidate is null)
        {
            SettingsStatus = "Steam library selection cancelled.";
            return;
        }
        _selectedSteamInstallation = candidate;
        WorkshopFolder = candidate.WorkshopFolder;
        LocalModsFolder = candidate.LocalModsFolder;
        SettingsStatus = candidates.Count == 1
            ? $"Detected RimWorld in {candidate.LibraryRoot}."
            : $"Detected {candidates.Count} RimWorld libraries; using {candidate.LibraryRoot}.";
        Append(SettingsStatus, ActivitySeverity.Success);

        if (saveWhenFound)
            await SaveSettingsFeatureAsync("settings.save-discovery", "Save Discovered Steam Settings");
    }

    private SteamInstallationCandidate? ChooseSteamInstallation(IReadOnlyList<SteamInstallationCandidate> candidates)
    {
        var selector = new ComboBox
        {
            ItemsSource = candidates,
            DisplayMemberPath = nameof(SteamInstallationCandidate.DisplayName),
            SelectedIndex = 0,
            MinWidth = 500,
            Margin = new Thickness(0, 12, 0, 18)
        };
        var accept = new Button { Content = "Use Selected Library", MinWidth = 150, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true, Margin = new Thickness(10, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(accept);
        buttons.Children.Add(cancel);
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = "Multiple RimWorld Steam libraries were found.", FontSize = 18, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Choose the library RimForge should use.", Foreground = (Brush)FindResource("TextMutedBrush"), Margin = new Thickness(0, 5, 0, 0) });
        panel.Children.Add(selector);
        panel.Children.Add(buttons);
        var dialog = new Window
        {
            Title = "Choose RimWorld Library",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("Bg2Brush"),
            Content = panel
        };
        accept.Click += (_, _) => { dialog.DialogResult = true; dialog.Close(); };
        return dialog.ShowDialog() == true ? selector.SelectedItem as SteamInstallationCandidate : null;
    }

    private async Task SaveSettingsCoreAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(RepositoryRoot, "Config.json");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken))?.AsObject() ?? new JsonObject();
        var rootFolders = new JsonArray(JsonValue.Create(WorkshopFolder), JsonValue.Create(LocalModsFolder));
        var officialContentFolder = _selectedSteamInstallation?.OfficialContentFolder;
        if (string.IsNullOrWhiteSpace(officialContentFolder) && !string.IsNullOrWhiteSpace(LocalModsFolder))
        {
            var expandedLocalMods = Environment.ExpandEnvironmentVariables(LocalModsFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var gameRoot = Directory.GetParent(expandedLocalMods)?.FullName;
            if (!string.IsNullOrWhiteSpace(gameRoot))
            {
                officialContentFolder = Path.Combine(gameRoot, "Data");
            }
        }
        if (!string.IsNullOrWhiteSpace(officialContentFolder) && Directory.Exists(officialContentFolder))
        {
            rootFolders.Add(JsonValue.Create(officialContentFolder));
        }
        root["RootFolders"] = rootFolders;
        root["OutputFolder"] = OutputFolderSetting;
        root["TargetRimWorldVersion"] = TargetRimWorldVersion;
        root["RimWorldExecutable"] = _selectedSteamInstallation?.GameExecutable;
        root["SteamExecutable"] = _selectedSteamInstallation?.SteamExecutable;
        root["ExternalTimeoutSeconds"] = Math.Clamp(ExternalTimeoutSeconds, 1, 300);
        root["Ui"] = new JsonObject
        {
            ["ShowForgeNarrative"] = ShowForgeNarrative,
            ["OpenConsoleOnGameLaunch"] = OpenConsoleOnGameLaunch,
            ["FirstRunGuideCompleted"] = _firstRunGuideCompleted,
            ["FirstRunGuideRevision"] = _firstRunGuideRevision,
            ["DependencyAssistanceMode"] = DependencyAssistancePreference.ToString(),
            ["OrphanCleanupMode"] = OrphanCleanupPreference.ToString(),
            ["LoadOrderSortingMode"] = LoadOrderSortingPreference.ToString(),
            ["LaterBandsOverrideEarlier"] = LaterBandsOverrideEarlier,
            ["ReconcileDependenciesDownwardOnly"] = ReconcileDependenciesDownwardOnly,
            ["CuratedAnchorsBidirectional"] = CuratedAnchorsBidirectional,
            ["LoadOrderRulePackId"] = _sortingPolicySettings.RulePackId
        };
        await File.WriteAllTextAsync(path, root.ToJsonString(RimForgeJson.Indented), cancellationToken);
    }

    private Task SaveSettingsFeatureAsync(string key, string displayName)
    {
        var settingsPath = Path.Combine(RepositoryRoot, "Config.json");
        return RunFeatureTaskAsync(
            key,
            displayName,
            context =>
            {
                context.Report(new BackgroundTaskProgress(
                    "Saving settings",
                    "Persisting RimForge configuration and workspace preferences.",
                    settingsPath,
                    null,
                    0,
                    1,
                    "Writing validated configuration",
                    settingsPath));
                return SaveSettingsCoreAsync(context.CancellationToken);
            });
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var path = Path.Combine(RepositoryRoot, "Config.json");
            var root = await RunFeatureTaskAsync(
                "settings.load",
                "Load Application Settings",
                async context =>
                {
                    context.Report(new BackgroundTaskProgress(
                        "Loading settings",
                        "Reading RimForge configuration and workspace preferences.",
                        path,
                        null,
                        0,
                        1,
                        "Validating saved application configuration",
                        path));
                    var json = await File.ReadAllTextAsync(path, context.CancellationToken);
                    var parsed = JsonNode.Parse(json)?.AsObject();
                    context.Report(new BackgroundTaskProgress(
                        "Settings loaded",
                        "Application settings are ready.",
                        path,
                        100,
                        1,
                        1,
                        "Saved workspace configuration discovered",
                        path));
                    return parsed;
                });
            var folders = root?["RootFolders"]?.AsArray();
            WorkshopFolder = folders?.ElementAtOrDefault(0)?.GetValue<string>() ?? string.Empty;
            LocalModsFolder = folders?.ElementAtOrDefault(1)?.GetValue<string>() ?? string.Empty;
            OutputFolderSetting = root?["OutputFolder"]?.GetValue<string>() ?? "Output";
            TargetRimWorldVersion = root?["TargetRimWorldVersion"]?.GetValue<string>() ?? "1.6";
            ExternalTimeoutSeconds = root?["ExternalTimeoutSeconds"]?.GetValue<int>() ?? 10;
            if (root?["Ui"] is JsonObject ui)
            {
                ShowForgeNarrative = ui["ShowForgeNarrative"]?.GetValue<bool>() ?? true;
                OpenConsoleOnGameLaunch = ui["OpenConsoleOnGameLaunch"]?.GetValue<bool>() ?? false;
                _firstRunGuideCompleted = ui["FirstRunGuideCompleted"]?.GetValue<bool>() ?? false;
                _firstRunGuideRevision = ui["FirstRunGuideRevision"]?.GetValue<int>()
                    ?? (_firstRunGuideCompleted ? 1 : 0);
                if (!Enum.TryParse(ui["DependencyAssistanceMode"]?.GetValue<string>(), true, out _dependencyAssistanceMode))
                    _dependencyAssistanceMode = DependencyAssistanceMode.Automatic;
                if (!Enum.TryParse(ui["OrphanCleanupMode"]?.GetValue<string>(), true, out _orphanCleanupMode))
                    _orphanCleanupMode = OrphanCleanupMode.Ask;
                if (!Enum.TryParse(ui["LoadOrderSortingMode"]?.GetValue<string>(), true, out LoadOrderSortingMode sortingMode))
                    sortingMode = LoadOrderSortingMode.CategoryFirst;
                _sortingPolicySettings = new SortingPolicySettings(
                    sortingMode,
                    ui["LaterBandsOverrideEarlier"]?.GetValue<bool>() ?? true,
                    ui["ReconcileDependenciesDownwardOnly"]?.GetValue<bool>() ?? true,
                    ui["CuratedAnchorsBidirectional"]?.GetValue<bool>() ?? true,
                    ui["LoadOrderRulePackId"]?.GetValue<string>() ?? "rimforge.category-first.v1");
                Notify(nameof(DependencyAssistancePreference));
                Notify(nameof(OrphanCleanupPreference));
                Notify(nameof(LoadOrderSortingPreference));
                Notify(nameof(LaterBandsOverrideEarlier));
                Notify(nameof(ReconcileDependenciesDownwardOnly));
                Notify(nameof(CuratedAnchorsBidirectional));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SettingsStatus = "Could not load settings: " + ex.Message;
        }
    }

    private async void Settings_SaveRequested(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(WorkshopFolder) || string.IsNullOrWhiteSpace(LocalModsFolder))
                throw new InvalidOperationException("Both mod folders are required.");
            await SaveSettingsFeatureAsync("settings.save", "Save Settings");
            SettingsStatus = "Settings saved.";
            Append("RimForge settings saved.", ActivitySeverity.Success);
            _notificationService.Enqueue(new NotificationRequest(
                "Settings saved",
                "RimForge configuration changes were saved successfully.",
                NotificationSeverity.Success));
        }
        catch (OperationCanceledException)
        {
            SettingsStatus = "Settings save cancelled.";
        }
        catch (Exception ex)
        {
            SettingsStatus = ex.Message;
            Append("Settings could not be saved: " + ex.Message, ActivitySeverity.Error);
            _notificationService.Enqueue(new NotificationRequest(
                "Settings save failed",
                ex.Message,
                NotificationSeverity.Error,
                [new NotificationAction("view-activity", "View Log")],
                TimeSpan.FromSeconds(12)));
        }
    }

}
