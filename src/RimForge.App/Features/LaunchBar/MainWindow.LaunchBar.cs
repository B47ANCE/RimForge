using System.IO;
using System.Windows;
using System.Windows.Media;
using RimForge.Analysis.Models;
using RimForge.Core.BackgroundTasks;
using RimForge.Core.Diagnostics;
using RimForge.Core.Models;
using RimForge.Core.Services;
using RimForge.UI.Dialogs;

namespace RimForge.App;

public partial class MainWindow
{
    private async void LaunchForgedProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = _forgedProfile;
        if (profile is not null)
            await ExecuteFeatureCommandAsync("Launch Forged Profile", () => LaunchProfileCoreAsync(profile));
    }

    private async void LaunchProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is not null)
            await ExecuteFeatureCommandAsync("Launch Profile", () => LaunchProfileCoreAsync(profile));
    }

    private async Task LaunchProfileCoreAsync(RimForgeProfile profile)
    {
        var candidates = await RunFeatureTaskAsync(
            "launch.discover-installation",
            "Locate RimWorld Installation",
            context =>
            {
                context.Report(new BackgroundTaskProgress(
                    "Locating RimWorld",
                    "Resolving the configured Steam library and game executable.",
                    "libraryfolders.vdf",
                    null,
                    0,
                    0,
                    "Validating launch targets",
                    string.Empty));
                context.CancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(_steamLibraryDiscoveryService.FindRimWorldInstallations());
            });
        var candidate = _selectedSteamInstallation ?? candidates
            .FirstOrDefault(item => item.WorkshopFolder.Equals(WorkshopFolder, StringComparison.OrdinalIgnoreCase)
                                 || item.LocalModsFolder.Equals(LocalModsFolder, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault();
        // Launch Readiness is advisory and profile-specific. Build a projection from the existing
        // shared analysis snapshot; this performs no scan, file read, or analysis refresh.
        var scopedIssues = _analysisSnapshot is null
            ? Array.Empty<IssueWorkItem>()
            : _issueEngine.Build(
                _analysisSnapshot,
                IssueScopeKind.ActiveProfile,
                $"Profile: {profile.Name}",
                Mods.ToList(),
                profile.ActiveMods.ToArray(),
                IssueIgnoreStore.Snapshot()).ActiveIssues.ToArray();
        var errorCount = scopedIssues.Count(issue => issue.Severity == AnalysisIssueSeverity.Error);
        var warningCount = scopedIssues.Count(issue => issue.Severity == AnalysisIssueSeverity.Warning);
        var gameExecutable = candidate?.GameExecutable;
        var executableFound = !string.IsNullOrWhiteSpace(gameExecutable) && File.Exists(gameExecutable);
        var latestForgeState = ForgeSession.Status == ForgeSessionStatus.Completed
            ? (errorCount > 0 || warningCount > 0
                ? $"Completed with conditions for {ForgeSession.ProfileName}"
                : $"Completed for {ForgeSession.ProfileName}")
            : null;

        var approved = ForgeDialogService.ShowLaunchReadinessReview(
            this,
            profile.Name,
            profile.ActiveMods.Count,
            errorCount,
            warningCount,
            hasSavedProfile: File.Exists(profile.ModsConfigPath),
            executableFound: executableFound,
            executablePath: gameExecutable,
            latestForgeState: latestForgeState,
            hasUnsavedChanges: IsLoadOrderDirty && ReferenceEquals(profile, SelectedProfile));
        if (!approved)
        {
            Append("Launch Readiness Review was aborted.", ActivitySeverity.Info);
            return;
        }

        var activation = await RunFeatureTaskAsync(
            "launch.activate-profile",
            "Activate Launch Profile",
            context =>
            {
                context.Report(new BackgroundTaskProgress(
                    "Activating launch profile",
                    $"Preparing '{profile.Name}' for RimWorld.",
                    profile.ModsConfigPath,
                    null,
                    0,
                    profile.ActiveMods.Count,
                    "Creating a recoverable ModsConfig.xml activation",
                    profile.ModsConfigPath));
                return _profileWorkspaceService.ActivateAsync(profile, context.CancellationToken);
            });
        if (!activation.Success)
        {
            Append(activation.Message, ActivitySeverity.Error);
            StatusText = "Activation failed";
            StatusBrush = (Brush)FindResource("DangerBrush");
            _notificationService.Enqueue(new NotificationRequest(
                "Profile activation failed",
                activation.Message,
                NotificationSeverity.Error,
                [new NotificationAction("view-activity", "View Log")],
                TimeSpan.FromSeconds(12)));
            return;
        }

        var result = await RunFeatureTaskAsync(
            "launch.start-game",
            "Launch RimWorld",
            context =>
            {
                context.Report(new BackgroundTaskProgress(
                    "Launching RimWorld",
                    $"Starting RimWorld with profile '{profile.Name}'.",
                    gameExecutable ?? candidate?.SteamExecutable ?? string.Empty,
                    null,
                    0,
                    0,
                    "Waiting for the game process to start",
                    gameExecutable ?? string.Empty));
                return _gameLaunchService.LaunchAsync(
                    new GameLaunchRequest(profile, candidate?.SteamExecutable, gameExecutable),
                    context.CancellationToken);
            });
        Append(result.Message, result.Success ? ActivitySeverity.Success : ActivitySeverity.Error);
        StatusText = result.Success ? $"Launching {profile.Name}" : "Launch failed";
        StatusBrush = (Brush)FindResource(result.Success ? "SuccessBrush" : "DangerBrush");
        _notificationService.Enqueue(new NotificationRequest(
            result.Success ? "RimWorld launched" : "Launch failed",
            result.Message,
            result.Success ? NotificationSeverity.Success : NotificationSeverity.Error,
            result.Success ? null : [new NotificationAction("view-activity", "View Log")],
            TimeSpan.FromSeconds(result.Success ? 7 : 12)));

        if (result.Success && OpenConsoleOnGameLaunch)
        {
            ScrollToWorkspaceSection(ConsolePanel, "Console");
            ConsoleFeature.SelectGameLogTab();
            _gameLogAutoFollow = true;
            if (GameLogEntries.Count > 0)
                ConsoleFeature.ScrollGameLogIntoView(GameLogEntries[^1]);
            Append("Opened the active RimWorld Player.log stream.", ActivitySeverity.Success);
        }
    }

}
