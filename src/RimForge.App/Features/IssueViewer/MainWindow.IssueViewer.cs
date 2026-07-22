using System;
using System.Linq;
using System.IO;
using System.Windows;
using RimForge.Analysis.Models;
using RimForge.Analysis.Services;
using RimForge.App.Features.IssueViewer;
using RimForge.Core.BackgroundTasks;
using RimForge.Core.Models;
using RimForge.UI.Dialogs;

namespace RimForge.App;

public partial class MainWindow
{
    private IssueIgnoreStore? _issueIgnoreStore;

    private IssueIgnoreStore IssueIgnoreStore => _issueIgnoreStore ??= new IssueIgnoreStore(
        Path.Combine(RepositoryRoot, "Output", "State", "ignored-issues.json"));

    private void RebuildIssueViewer()
    {
        if (_analysisSnapshot is null)
        {
            IssueItems.ReplaceAll(Array.Empty<IssueWorkItem>());
            _issueViewerSnapshot = null;
        }
        else
        {
            var scope = ShowFullLibrary ? IssueScopeKind.FullLibrary : IssueScopeKind.ActiveProfile;
            var scopeName = ShowFullLibrary
                ? "Full Library"
                : SelectedProfile is null ? "No Active Profile" : $"Profile: {SelectedProfile.Name}";
            var activeIds = SelectedProfile?.ActiveMods.ToArray();
            _issueViewerSnapshot = _issueEngine.Build(_analysisSnapshot, scope, scopeName, Mods.ToList(), activeIds, IssueIgnoreStore.Snapshot());
            IssueItems.ReplaceAll(_issueViewerSnapshot.Issues);
        }

        SelectedIssueItem = IssueItems.FirstOrDefault();
        Notify(nameof(IssueViewerScopeText));
        Notify(nameof(IssueViewerStatusText));
        Notify(nameof(HasIssueViewerItems));
        Notify(nameof(HasAutoFixableIssues));
    }

    private async void IssueViewer_FixSelectedRequested(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Apply Issue Repair", () => FixSelectedIssueAsync(sender, e));

    private async Task FixSelectedIssueAsync(object sender, RoutedEventArgs e)
    {
        var issue = (e.OriginalSource as FrameworkElement)?.DataContext as IssueWorkItem ?? SelectedIssueItem;
        if (issue is null || issue.IsIgnored) return;
        var plan = _repairPlanner.Build(issue, Mods.ToList());
        if (!issue.CanAutoFix || plan.Status != RepairPlanStatus.Ready || plan.ExecutionMode != RepairExecutionMode.Automatic)
        {
            ForgeDialogService.ShowRepairPreview(this, plan.Title, plan.Summary,
                plan.Steps.Select(step => $"{step.Order}. {step.Action}: {step.TargetName}").ToArray(),
                plan.ChoicePackageIds.Select(GetDisplayNameForPackageId).ToArray(), plan.ExpectedResult);
            return;
        }

        if (!ForgeDialogService.ShowConfirmation(this, plan.Title, plan.Summary, "Apply Repair")) return;
        await ExecuteAutomaticRepairAsync(issue);
    }

    private async Task ExecuteAutomaticRepairAsync(IssueWorkItem issue)
    {
        if (issue.RepairAction != RepairActionKind.ReorderProfile || SelectedProfile is null) return;
        var changed = ApplyOptimizedLoadOrder(false);
        if (changed)
        {
            var profile = SelectedProfile;
            var activeMods = ActiveProfileMods.Select(item => item.PackageId).ToArray();
            var result = await RunFeatureTaskAsync(
                "issue.repair-load-order",
                "Apply Issue Repair",
                context =>
                {
                    context.Report(new BackgroundTaskProgress(
                        "Applying issue repair",
                        $"Saving the repaired load order for '{profile.Name}'.",
                        profile.ModsConfigPath,
                        null,
                        0,
                        activeMods.Length,
                        issue.Title,
                        profile.ModsConfigPath));
                    return _profileWorkspaceService.SaveLoadOrderAsync(profile, activeMods, context.CancellationToken);
                });
            if (!result.Success || result.UpdatedProfile is null)
            {
                Append($"Repair failed: {result.Message}", RimForge.Core.Models.ActivitySeverity.Error);
                return;
            }
            var index = Profiles.IndexOf(SelectedProfile);
            if (index >= 0) Profiles[index] = result.UpdatedProfile;
            RefreshLibraryProfileWorkspace();
            SelectedProfile = result.UpdatedProfile;
            IsLoadOrderDirty = false;
        }
        await RefreshAnalysisSnapshot();
        RebuildModSorter();
        RebuildProfileLoadOrder();
        Append($"Repair completed and shared analysis state refreshed for {issue.ModName}.", RimForge.Core.Models.ActivitySeverity.Success);
    }

    private async Task RefreshAnalysisSnapshot()
    {
        var refreshTask = _analysisRefreshTask;
        if (refreshTask is null)
        {
            QueueAnalysisRefresh();
            refreshTask = _analysisRefreshTask;
        }

        if (refreshTask is not null)
            await refreshTask;
    }

    private void IssueViewer_ToggleIgnoreRequested(object sender, RoutedEventArgs e)
    {
        var issue = (e.OriginalSource as FrameworkElement)?.DataContext as IssueWorkItem ?? SelectedIssueItem;
        if (issue is null) return;
        IssueIgnoreStore.SetIgnored(issue.Id, !issue.IsIgnored);
        RebuildIssueViewer();
        NotifyAnalysisProperties();
        RebuildModSorter();
        RebuildProfileLoadOrder();
    }

    private async void IssueViewer_FixAllRequested(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Apply Automatic Issue Repairs", FixAllIssuesAsync);

    private async Task FixAllIssuesAsync()
    {
        var automatic = IssueItems
            .Where(issue => issue.CanAutoFix && !issue.IsIgnored)
            .Where(issue => _repairPlanner.Build(issue, Mods.ToList()) is
                { Status: RepairPlanStatus.Ready, ExecutionMode: RepairExecutionMode.Automatic })
            .ToArray();
        if (automatic.Length == 0) return;

        // Current automatic repairs converge through the canonical profile reorder. Apply once, then refresh once.
        if (!ForgeDialogService.ShowConfirmation(
                this,
                "Fix All Automatic Issues",
                $"RimForge found {automatic.Length} automatic repair plan(s). They converge through one canonical load-order update and one saved profile revision.",
                "Apply Automatic Repairs"))
            return;
        await ExecuteAutomaticRepairAsync(automatic[0]);
    }

    private void IssueViewer_ModNavigationRequested(object? sender, IssueModNavigationRequestedEventArgs e)
    {
        SelectModByPackageId(e.PackageId, ForgeGraphQueryOrigin.IssueNavigation);
        if (e.OpenForgeView)
            ScrollToWorkspaceSection(ForgeViewPanel, "ForgeView");
    }

    private string GetDisplayNameForPackageId(string packageId) =>
        Mods.FirstOrDefault(mod => string.Equals(mod.PackageId, packageId, StringComparison.OrdinalIgnoreCase))?.DisplayName
        ?? packageId;

}
