using System;
using System.Linq;
using System.IO;
using System.Windows;
using RimForge.Analysis.Models;
using RimForge.Analysis.Services;
using RimForge.App.Features.IssueViewer;
using RimForge.Core.BackgroundTasks;
using RimForge.Core.Models;
using RimForge.Core.Services;
using RimForge.UI.Dialogs;

namespace RimForge.App;

public partial class MainWindow
{
    private IssueIgnoreStore? _issueIgnoreStore;
    private RepairTransactionExecutor? _repairTransactionExecutor;
    private bool _repairRecoveryInspected;
    private string _lastRepairOutcomeText = "No repair transaction has run in this session.";

    private IssueIgnoreStore IssueIgnoreStore => _issueIgnoreStore ??= new IssueIgnoreStore(
        Path.Combine(RepositoryRoot, "Output", "State", "ignored-issues.json"));
    private RepairTransactionExecutor RepairTransactionExecutor => _repairTransactionExecutor ??= new RepairTransactionExecutor(
        Path.Combine(RepositoryRoot, "Output", "State", "repair-transactions"));
    public string LastRepairOutcomeText
    {
        get => _lastRepairOutcomeText;
        private set { if (_lastRepairOutcomeText == value) return; _lastRepairOutcomeText = value; Notify(nameof(LastRepairOutcomeText)); }
    }

    private void RebuildIssueViewer()
    {
        InspectInterruptedRepairTransactions();
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
        var plan = BuildRepairPlan(issue);
        if (!issue.CanAutoFix || !plan.CanExecute || plan.ExecutionMode != RepairExecutionMode.Automatic)
        {
            ForgeDialogService.ShowRepairPreview(this, plan.Title, plan.Summary,
                BuildRepairPreviewDetails(plan),
                plan.ChoicePackageIds.Select(GetDisplayNameForPackageId).ToArray(), plan.ExpectedResult);
            return;
        }

        if (!ForgeDialogService.ShowConfirmation(this, plan.Title, plan.Summary, "Apply Repair")) return;
        await ExecuteAutomaticRepairAsync(issue);
    }

    private async Task ExecuteAutomaticRepairAsync(IssueWorkItem issue)
    {
        var plan = BuildRepairPlan(issue);
        if (!plan.CanExecute)
        {
            var failures = plan.Preconditions.Where(item => !item.IsSatisfied).Select(item => item.FailureMessage).ToArray();
            ForgeDialogService.ShowRepairPreview(this, plan.Title, plan.Summary,
                BuildRepairPreviewDetails(plan),
                failures, plan.ExpectedResult);
            Append($"Repair blocked before mutation: {string.Join(" ", failures)}", ActivitySeverity.Warning);
            return;
        }
        if (issue.RepairAction != RepairActionKind.ReorderProfile || SelectedProfile is null) return;
        var profile = SelectedProfile;
        var proposedOrder = CalculateCanonicalLoadOrder().ToArray();
        LoadOrderSaveResult? saveResult = null;
        var execution = await RunFeatureTaskAsync(
            "issue.repair-transaction",
            "Apply Transactional Repair",
            context => RepairTransactionExecutor.ExecuteAsync(
                plan,
                async cancellationToken =>
                {
                    context.Report(new BackgroundTaskProgress(
                        "Applying transactional repair",
                        $"Saving the repaired load order for '{profile.Name}'.",
                        profile.ModsConfigPath,
                        null,
                        0,
                        proposedOrder.Length,
                        issue.Title,
                        profile.ModsConfigPath));
                    saveResult = await _profileWorkspaceService.SaveLoadOrderAsync(profile, proposedOrder, cancellationToken);
                    return new RepairMutationResult(saveResult.Success, saveResult.Message, saveResult.BackupPath);
                },
                (_, _) => Task.FromResult(new RepairMutationResult(
                    true,
                    "The profile workspace service restored its atomic save backups.")),
                context.CancellationToken));

        LastRepairOutcomeText = $"{execution.State}: {execution.Message} • Transaction {execution.Journal.Id}";
        var severity = execution.Success ? NotificationSeverity.Success
            : execution.State == RepairTransactionState.RecoveryRequired ? NotificationSeverity.Error
            : NotificationSeverity.Warning;
        _notificationService.Enqueue(new NotificationRequest(
            execution.Success ? "Repair committed" : "Repair not applied",
            LastRepairOutcomeText,
            severity,
            [new NotificationAction("view-activity", "View Audit")],
            TimeSpan.FromSeconds(12)));
        Append(LastRepairOutcomeText, execution.Success ? ActivitySeverity.Success : ActivitySeverity.Warning);

        if (!execution.Success)
        {
            RebuildProfileLoadOrder();
            return;
        }

        if (saveResult?.UpdatedProfile is not null)
        {
            var index = Profiles.IndexOf(SelectedProfile);
            if (index >= 0) Profiles[index] = saveResult.UpdatedProfile;
            RefreshLibraryProfileWorkspace();
            SelectedProfile = saveResult.UpdatedProfile;
            IsLoadOrderDirty = false;
        }
        await RefreshAnalysisSnapshot();
        RebuildModSorter();
        RebuildProfileLoadOrder();
        ForgeViewFeature.SynchronizeSelection(issue.PackageId, ForgeGraphQueryOrigin.IssueNavigation);
        Notify(nameof(SelectedProfileReadiness));
        Notify(nameof(ForgeFocusedProvenanceSummary));
        Append($"Repair completed and shared analysis state refreshed for {issue.ModName}.", RimForge.Core.Models.ActivitySeverity.Success);
    }

    private void InspectInterruptedRepairTransactions()
    {
        if (_repairRecoveryInspected) return;
        _repairRecoveryInspected = true;
        var interrupted = RepairTransactionExecutor.DiscoverInterrupted();
        if (interrupted.Count == 0) return;
        LastRepairOutcomeText = $"Recovery required for {interrupted.Count} interrupted repair transaction(s). No new repair will reuse their state.";
        Append(LastRepairOutcomeText, ActivitySeverity.Warning);
        _notificationService.Enqueue(new NotificationRequest(
            "Repair recovery required",
            LastRepairOutcomeText,
            NotificationSeverity.Warning,
            [new NotificationAction("view-activity", "View Audit")],
            TimeSpan.FromSeconds(15)));
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
            .Where(issue => BuildRepairPlan(issue) is
                { CanExecute: true, ExecutionMode: RepairExecutionMode.Automatic })
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

    private RepairPlan BuildRepairPlan(IssueWorkItem issue, string? selectedCycleFirstPackageId = null) =>
        _repairPlanner.Build(issue, Mods.ToList(), selectedCycleFirstPackageId, RepairPlanner.CaptureContext(SelectedProfile));

    private static IReadOnlyList<string> BuildRepairPreviewDetails(RepairPlan plan) =>
    [
        $"Confidence: {plan.Confidence} • Safety: {plan.SafetyClass}",
        $"Evidence: {string.Join("; ", plan.Evidence.Select(item => item.Summary))}",
        .. plan.Preconditions.Select(item => $"Precondition {(item.IsSatisfied ? "ready" : "blocked")}: {item.Target}"),
        .. plan.Steps.Select(step => $"{step.Order}. {step.Action}: {step.TargetName}")
    ];

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
