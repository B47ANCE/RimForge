using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RimForge.Core.BackgroundTasks;
using RimForge.App.Features.ModSorter;
using RimForge.Core.Models;
using RimForge.Core.Services;
using RimForge.UI.Dialogs;
using RimForge.UI.ViewModels;

namespace RimForge.App;

public partial class MainWindow
{
    private enum DependencyRemovalDecision
    {
        Cancel,
        RemoveDependents,
        KeepDependents
    }

    private sealed record LoadOrderUndoSnapshot(
        string? ProfileName,
        ProfileLoadOrderItemViewModel[] ActiveItems,
        ProfileLoadOrderItemViewModel[] InactiveItems,
        bool WasDirty,
        ProfileLoadOrderItemViewModel? SelectedItem)
    {
        public string[] ActivePackageIds => ActiveItems.Select(item => item.PackageId).ToArray();
        public string[] InactivePackageIds => InactiveItems.Select(item => item.PackageId).ToArray();
    }

    private LoadOrderUndoSnapshot CaptureLoadOrderUndoSnapshot() => new(
        SelectedProfile?.Name,
        ActiveProfileMods.ToArray(),
        InactiveInstalledMods.ToArray(),
        IsLoadOrderDirty,
        SelectedLoadOrderItem);

    private void RegisterLoadOrderUndo(string description, LoadOrderUndoSnapshot before)
    {
        var activeNow = ActiveProfileMods.Select(item => item.PackageId).ToArray();
        var inactiveNow = InactiveInstalledMods.Select(item => item.PackageId).ToArray();
        if (activeNow.SequenceEqual(before.ActivePackageIds, StringComparer.OrdinalIgnoreCase) &&
            inactiveNow.SequenceEqual(before.InactivePackageIds, StringComparer.OrdinalIgnoreCase))
            return;

        _undoService.Register(description, () => RestoreLoadOrderUndoSnapshot(before));
    }

    private void RestoreLoadOrderUndoSnapshot(LoadOrderUndoSnapshot snapshot)
    {
        if (SelectedProfile is null || !string.Equals(SelectedProfile.Name, snapshot.ProfileName, StringComparison.OrdinalIgnoreCase))
            return;

        ActiveProfileMods.Clear();
        foreach (var item in snapshot.ActiveItems) ActiveProfileMods.Add(item);

        InactiveInstalledMods.Clear();
        foreach (var item in snapshot.InactiveItems) InactiveInstalledMods.Add(item);

        SyncCombinedLoadOrder(normalizeActiveOrder: false);
        IsLoadOrderDirty = snapshot.WasDirty;
        SelectedLoadOrderItem = snapshot.SelectedItem is not null &&
                                (ActiveProfileMods.Contains(snapshot.SelectedItem) || InactiveInstalledMods.Contains(snapshot.SelectedItem))
            ? snapshot.SelectedItem
            : null;
    }

    private void ModSorter_SelectionRequested(object? sender, ModSorterSelectionEventArgs e) =>
        DashboardModList_SelectionChanged(e.List, e.Original);

    private void ModSorter_DragStartRequested(object? sender, ModSorterMouseButtonEventArgs e) =>
        ProfileModList_PreviewMouseLeftButtonDown(e.List, e.Original);

    private void ModSorter_DragMoveRequested(object? sender, ModSorterMouseEventArgs e) =>
        ProfileModList_PreviewMouseMove(e.List, e.Original);

    private void ModSorter_ActiveDropRequested(object? sender, ModSorterDragEventArgs e) =>
        ActiveProfileMods_Drop(e.List, e.Original);

    private void ModSorter_InactiveDropRequested(object? sender, ModSorterDragEventArgs e) =>
        InactiveInstalledMods_Drop(e.List, e.Original);

    private void ModSorter_SearchSteamLibrariesRequested(object sender, RoutedEventArgs e) =>
        Settings_SearchSteamLibrariesRequested(sender, e);

    private void ModSorter_OpenSettingsRequested(object sender, RoutedEventArgs e) =>
        Settings_Click(sender, e);

    private void ModSorter_HealthNavigationRequested(object? sender, ModHealthNavigationRequestedEventArgs e)
    {
        var item = e.Item;
        SelectedMod = item.Mod;
        if (item.Mod is null) return;

        if (!item.IsEnabled && !ShowFullLibrary)
            ShowFullLibrary = true;

        var issue = IssueItems
            .Where(candidate => string.Equals(candidate.PackageId, item.PackageId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.IsIgnored)
            .ThenByDescending(candidate => candidate.Severity)
            .FirstOrDefault();

        if (issue is null)
        {
            Append($"No Issue Viewer details are available for {item.DisplayName} in the current analysis generation.", ActivitySeverity.Info);
            return;
        }

        SelectedIssueItem = issue;
        ShowPage(IssueViewerWorkspacePanel, "Issue Viewer");
        IssueViewerFeature.FocusIssue(issue);
    }

    private void DashboardModList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is not ProfileLoadOrderItemViewModel item)
        {
            return;
        }

        SelectedMod = item.Mod;

        if (ReferenceEquals(listBox, ModSorterFeature.ActiveList) && !ReferenceEquals(SelectedLoadOrderItem, item))
        {
            SelectedLoadOrderItem = item;
        }
    }

    private void ProfileModList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _loadOrderDragStart = e.GetPosition(null);
        if (sender is not ListBox list || e.OriginalSource is not DependencyObject origin) return;
        if (ItemsControl.ContainerFromElement(list, origin) is not ListBoxItem container) return;
        var modifiers = Keyboard.Modifiers;
        var extendingSelection = (modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != ModifierKeys.None;
        if (!container.IsSelected)
        {
            if (!extendingSelection)
            {
                list.SelectedItems.Clear();
                container.IsSelected = true;
            }
            return;
        }

        // WPF normally collapses an extended selection when a selected row is pressed.
        // Preserve the group so dragging any selected row moves the complete selection.
        if (!extendingSelection && list.SelectedItems.Count > 1)
            e.Handled = true;
    }

    private void ProfileModList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || sender is not ListBox list) return;
        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _loadOrderDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _loadOrderDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var sourceOrder = ReferenceEquals(list, ModSorterFeature.ActiveList)
            ? ActiveProfileMods.ToArray()
            : InactiveInstalledMods.ToArray();
        var selected = list.SelectedItems.Cast<ProfileLoadOrderItemViewModel>().ToHashSet();
        var items = sourceOrder.Where(selected.Contains).ToArray();
        if (items.Length == 0) return;
        if (items.Any(item => item.IsMandatory))
        {
            Append("Core is required by RimWorld and cannot be dragged or deactivated.", ActivitySeverity.Warning);
            return;
        }

        foreach (var item in items) item.IsDragGhost = true;
        ModSorterFeature.BeginDragVisual(items);
        try
        {
            var payload = new ModDragPayload(items, ReferenceEquals(list, ModSorterFeature.ActiveList));
            DragDrop.DoDragDrop(list, new DataObject(typeof(ModDragPayload), payload), DragDropEffects.Move);
        }
        finally
        {
            foreach (var item in items) item.IsDragGhost = false;
            ModSorterFeature.EndDragVisual();
        }
    }

    private void ActiveProfileMods_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ModDragPayload)) is not ModDragPayload payload ||
            SelectedProfile is null || sender is not ListBox list || payload.Items.Count == 0) return;
        var items = payload.Items
            .GroupBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (payload.FromActive && items.Any(item => item.IsLoadOrderAnchor))
        {
            Append("Canonical position anchors cannot be manually reordered while active. Drag them to Inactive to disable them instead.", ActivitySeverity.Warning);
            return;
        }
        if (SelectedProfile.IsLocked && items.Any(item => !item.IsOfficialDlc))
        {
            Append($"{SelectedProfile.Name} is locked. Only official DLC can be changed in this profile.", ActivitySeverity.Warning);
            return;
        }

        var insertionIndex = ModSorterFeature.GetInsertionIndex(list, e);
        var topAnchorCount = ActiveProfileMods.TakeWhile(item => item.IsTopLoadOrderAnchor).Count();
        var bottomAnchorStart = ActiveProfileMods.Count - ActiveProfileMods.Reverse().TakeWhile(item => item.IsBottomLoadOrderAnchor).Count();
        if (insertionIndex < topAnchorCount || insertionIndex > bottomAnchorStart)
        {
            Append("Drop rejected: mods must remain between RimForge's canonical top and bottom load-order anchors.", ActivitySeverity.Warning);
            return;
        }

        var before = CaptureLoadOrderUndoSnapshot();
        var originalActive = ActiveProfileMods.ToArray();
        var activated = items.Where(InactiveInstalledMods.Contains).ToArray();

        ProfileLoadOrderItemViewModel[] addedDependencies;
        try
        {
            foreach (var item in items)
            {
                InactiveInstalledMods.Remove(item);
                ActiveProfileMods.Remove(item);
            }

            if (payload.FromActive)
            {
                var removedBefore = originalActive.Take(Math.Min(insertionIndex, originalActive.Length)).Count(item => items.Contains(item));
                insertionIndex -= removedBefore;
            }
            insertionIndex = Math.Clamp(insertionIndex, 0, ActiveProfileMods.Count);

            foreach (var item in items)
            {
                item.IsEnabled = true;
                ActiveProfileMods.Insert(Math.Clamp(insertionIndex++, 0, ActiveProfileMods.Count), item);
            }

            if (IsInstantAutoSortEnabled) ApplyOptimizedLoadOrder(false, registerUndo: false);
            addedDependencies = activated.Length > 0
                ? ResolveDependencyAssistanceForGroup(activated).ToArray()
                : Array.Empty<ProfileLoadOrderItemViewModel>();
            SyncCombinedLoadOrder();
            SelectedLoadOrderItem = items[0];
        }
        catch (Exception ex)
        {
            RestoreLoadOrderUndoSnapshot(before);
            var failure = $"The {items.Length}-mod move failed and the previous load order was restored: {ex.Message}";
            Append(failure, ActivitySeverity.Error);
            _notificationService.Enqueue(new NotificationRequest(
                "Mod group move failed",
                failure,
                NotificationSeverity.Error,
                [new NotificationAction("view-activity", "View Details")],
                TimeSpan.FromSeconds(12)));
            return;
        }

        RegisterLoadOrderUndo(items.Length == 1 ? $"Move {items[0].DisplayName}" : $"Move {items.Length} mods", before);
        PublishDependencyAssistanceNotification(activated, addedDependencies);
        ModSorterFeature.SelectItems(ModSorterFeature.ActiveList, items);

        var message = activated.Length > 0
            ? $"Activated {items.Length} mod{(items.Length == 1 ? string.Empty : "s")} in {SelectedProfile.Name}."
            : $"Reordered {items.Length} mod{(items.Length == 1 ? string.Empty : "s")} in {SelectedProfile.Name}.";
        Append(message, ActivitySeverity.Success);
        if (items.Length > 1)
            _notificationService.Enqueue(new NotificationRequest("Mod group moved", message, NotificationSeverity.Success, Duration: TimeSpan.FromSeconds(7)));
    }

    private void ActivateInactiveMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox ||
            checkBox.DataContext is not ProfileLoadOrderItemViewModel item ||
            SelectedProfile is null ||
            !InactiveInstalledMods.Contains(item))
        {
            return;
        }

        if (SelectedProfile.IsLocked && !item.IsOfficialDlc)
        {
            Append($"{SelectedProfile.Name} is locked. Only official DLC can be changed in this profile.", ActivitySeverity.Warning);
            checkBox.IsChecked = false;
            return;
        }

        var before = CaptureLoadOrderUndoSnapshot();
        InactiveInstalledMods.Remove(item);
        item.IsEnabled = true;
        var insertionIndex = GetOptimizedInsertionIndex(item);
        ActiveProfileMods.Insert(insertionIndex, item);
        var addedDependencies = ResolveDependencyAssistance(item);
        SyncCombinedLoadOrder();
        SelectedLoadOrderItem = item;

        RegisterLoadOrderUndo($"Enable {item.DisplayName}", before);
        PublishDependencyAssistanceNotification(item, addedDependencies);
        Append(
            $"Added {item.DisplayName} to {SelectedProfile.Name} at load-order position #{item.Position}. Save the active mod load order to keep this change.",
            ActivitySeverity.Success);
    }

    private IReadOnlyList<ProfileLoadOrderItemViewModel> ResolveDependencyAssistance(ProfileLoadOrderItemViewModel activatedItem) =>
        ResolveDependencyAssistanceForGroup(new[] { activatedItem });

    private IReadOnlyList<ProfileLoadOrderItemViewModel> ResolveDependencyAssistanceForGroup(
        IReadOnlyList<ProfileLoadOrderItemViewModel> activatedItems)
    {
        if (DependencyAssistancePreference == DependencyAssistanceMode.Manual || activatedItems.Count == 0)
            return Array.Empty<ProfileLoadOrderItemViewModel>();

        var installedItems = ActiveProfileMods.Concat(InactiveInstalledMods)
            .GroupBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var plan = _dependencyManagementService.PlanActivation(
            Mods.ToList(),
            ActiveProfileMods.Select(item => item.PackageId).ToArray(),
            activatedItems.Select(item => item.PackageId).ToArray());

        PublishMissingDependencyNotification(activatedItems, plan.MissingDependencies);
        if (!plan.HasInstalledDependencies) return Array.Empty<ProfileLoadOrderItemViewModel>();

        var ordered = plan.DependencyPackageIds
            .Select(packageId => installedItems.GetValueOrDefault(packageId))
            .Where(item => item is not null && InactiveInstalledMods.Contains(item))
            .Cast<ProfileLoadOrderItemViewModel>()
            .ToArray();
        if (ordered.Length == 0) return ordered;

        if (DependencyAssistancePreference == DependencyAssistanceMode.Ask)
        {
            var names = string.Join(Environment.NewLine, ordered.Select(item => "• " + item.DisplayName));
            var subject = activatedItems.Count == 1 ? activatedItems[0].DisplayName : $"{activatedItems.Count} activated mods";
            if (!ForgeDialogService.ShowConfirmation(
                    this,
                    "Add Required Dependencies",
                    $"{subject} requires these installed dependencies:\n\n{names}\n\nAdd them to the active load order?",
                    "Add Dependencies"))
                return Array.Empty<ProfileLoadOrderItemViewModel>();
        }

        foreach (var dependencyItem in ordered)
        {
            if (!InactiveInstalledMods.Remove(dependencyItem)) continue;
            dependencyItem.IsEnabled = true;
            ActiveProfileMods.Insert(
                Math.Clamp(GetOptimizedInsertionIndex(dependencyItem), 0, ActiveProfileMods.Count),
                dependencyItem);
        }
        return ordered;
    }

    private void PublishMissingDependencyNotification(
        IReadOnlyList<ProfileLoadOrderItemViewModel> activatedItems,
        IReadOnlyList<MissingDependencyRequirement> missing)
    {
        if (missing.Count == 0) return;
        var names = string.Join(", ", missing.Select(item => item.DisplayName).Take(4));
        var suffix = missing.Count > 4 ? $" and {missing.Count - 4} more" : string.Empty;
        var subject = activatedItems.Count == 1 ? activatedItems[0].DisplayName : "The activated mod group";
        var message = $"{subject} requires {names}{suffix}, but they are not installed.";
        _notificationService.Enqueue(new NotificationRequest(
            "Missing dependencies",
            message,
            NotificationSeverity.Warning,
            [new NotificationAction("view-activity", "View Details")],
            TimeSpan.FromSeconds(14)));
        Append(message, ActivitySeverity.Warning);
        foreach (var requirement in missing)
            Append($"Missing dependency path: {string.Join(" → ", requirement.Path)}", ActivitySeverity.Info);
    }

    private void PublishDependencyAssistanceNotification(
        ProfileLoadOrderItemViewModel activatedItem,
        IReadOnlyList<ProfileLoadOrderItemViewModel> addedDependencies) =>
        PublishDependencyAssistanceNotification(new[] { activatedItem }, addedDependencies);

    private void PublishDependencyAssistanceNotification(
        IReadOnlyList<ProfileLoadOrderItemViewModel> activatedItems,
        IReadOnlyList<ProfileLoadOrderItemViewModel> addedDependencies)
    {
        if (addedDependencies.Count == 0) return;
        var dependencyNames = addedDependencies.Select(item => item.DisplayName).ToArray();
        var subject = activatedItems.Count == 1 ? activatedItems[0].DisplayName : $"{activatedItems.Count} activated mods";
        var message = dependencyNames.Length <= 3
            ? $"Added {string.Join(", ", dependencyNames)} for {subject}."
            : $"Added {dependencyNames.Length} required dependencies for {subject}.";
        _notificationService.Enqueue(new NotificationRequest(
            "Dependencies added",
            message,
            NotificationSeverity.Success,
            [new NotificationAction("undo", "Undo"), new NotificationAction("view-activity", "View Details")],
            TimeSpan.FromSeconds(10)));
        Append(message, ActivitySeverity.Success);
    }

    private int GetOptimizedInsertionIndex(ProfileLoadOrderItemViewModel item)
    {
        if (ActiveProfileMods.Count == 0) return 0;

        var normalized = LoadOrderRules.Normalize(ActiveProfileMods.Select(active => active.PackageId));
        var topAnchorCount = normalized.TakeWhile(LoadOrderRules.IsTopAnchor).Count();
        var bottomAnchorCount = normalized.Reverse().TakeWhile(LoadOrderRules.IsBottomAnchor).Count();
        var minimumIndex = topAnchorCount;
        var maximumIndex = Math.Max(minimumIndex, ActiveProfileMods.Count - bottomAnchorCount);

        if (_analysisSnapshot is not null)
        {
            var dependencies = _analysisSnapshot.GetDependencies(item.PackageId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var dependents = _analysisSnapshot.GetDependents(item.PackageId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < ActiveProfileMods.Count; index++)
            {
                var activeId = ActiveProfileMods[index].PackageId;
                if (dependencies.Contains(activeId)) minimumIndex = Math.Max(minimumIndex, index + 1);
                if (dependents.Contains(activeId)) maximumIndex = Math.Min(maximumIndex, index);
            }

            var proposedRank = _analysisSnapshot.ProposedOrder.OrderedPackageIds
                .Select((packageId, index) => (packageId, index))
                .GroupBy(pair => pair.packageId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Min(pair => pair.index),
                    StringComparer.OrdinalIgnoreCase);

            if (proposedRank.TryGetValue(item.PackageId, out var itemRank))
            {
                var rankedIndex = ActiveProfileMods.Count;
                for (var index = 0; index < ActiveProfileMods.Count; index++)
                {
                    if (proposedRank.TryGetValue(ActiveProfileMods[index].PackageId, out var activeRank) && activeRank > itemRank)
                    {
                        rankedIndex = index;
                        break;
                    }
                }

                return Math.Clamp(rankedIndex, minimumIndex, Math.Max(minimumIndex, maximumIndex));
            }
        }

        return Math.Clamp(minimumIndex, 0, ActiveProfileMods.Count);
    }

    private void InactiveInstalledMods_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ModDragPayload)) is not ModDragPayload payload ||
            SelectedProfile is null || payload.Items.Count == 0) return;
        var items = payload.Items
            .GroupBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (items.Any(item => LoadOrderRules.IsCore(item.PackageId)))
        {
            const string message = "Core is required by RimWorld and cannot be deactivated.";
            Append(message, ActivitySeverity.Warning);
            _notificationService.Enqueue(new NotificationRequest("Core cannot be disabled", message, NotificationSeverity.Warning));
            return;
        }
        if (SelectedProfile.IsLocked && items.Any(item => !item.IsOfficialDlc))
        {
            Append($"{SelectedProfile.Name} is locked. Only official DLC can be changed in this profile.", ActivitySeverity.Warning);
            return;
        }

        var moving = items.Where(ActiveProfileMods.Contains).ToArray();
        if (moving.Length == 0) return;

        var impacts = GetActiveRemovalImpacts(moving);
        if (impacts.Length > 0)
        {
            var decision = ShowDependencyRemovalDecision(moving, impacts);
            if (decision == DependencyRemovalDecision.Cancel) return;
            if (decision == DependencyRemovalDecision.RemoveDependents)
            {
                var cascadeIds = moving.Select(item => item.PackageId)
                    .Concat(impacts.Select(impact => impact.PackageId))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var cascade = ActiveProfileMods
                    .Where(item => cascadeIds.Contains(item.PackageId, StringComparer.OrdinalIgnoreCase))
                    .Where(item => !LoadOrderRules.IsCore(item.PackageId))
                    .ToArray();
                DisableModGroup(cascade, offerOrphanCleanup: true);
                return;
            }

            IsInstantAutoSortEnabled = false;
            DisableModGroup(moving, offerOrphanCleanup: false);
            const string manualMessage = "Auto-Sort was disabled because a dependency was removed while dependent mods remain active.";
            Append(manualMessage, ActivitySeverity.Warning);
            _notificationService.Enqueue(new NotificationRequest("Manual load order enabled", manualMessage, NotificationSeverity.Warning, Duration: TimeSpan.FromSeconds(12)));
            return;
        }

        DisableModGroup(moving, offerOrphanCleanup: true);
    }

    private DependencyRemovalDecision ShowDependencyRemovalDecision(
        IReadOnlyCollection<ProfileLoadOrderItemViewModel> moving,
        IReadOnlyCollection<DependencyReason> impacts)
    {
        var dependencyNames = string.Join(", ", moving.Select(item => item.DisplayName).Take(3));
        var dependentNames = string.Join(Environment.NewLine, impacts.Select(item => "• " + item.DisplayName).Take(12));
        var suffix = impacts.Count > 12 ? $"\n• and {impacts.Count - 12} more" : string.Empty;
        var removeDependents = ForgeDialogService.ShowConfirmation(
            this,
            "Dependency Still Required",
            $"{dependencyNames} is still required by active mods:\n\n{dependentNames}{suffix}\n\n" +
            "Remove the dependency and every active mod that requires it?",
            "Remove All Impacted Mods",
            danger: true);
        if (removeDependents) return DependencyRemovalDecision.RemoveDependents;

        var keepDependents = ForgeDialogService.ShowConfirmation(
            this,
            "Keep Dependent Mods Active",
            "Move the dependency anyway and switch Auto-Sort to Manual? " +
            "The remaining dependent mods may not load correctly.",
            "Keep Dependents Active",
            danger: true);
        return keepDependents ? DependencyRemovalDecision.KeepDependents : DependencyRemovalDecision.Cancel;
    }

    private DependencyReason[] GetActiveRemovalImpacts(IReadOnlyCollection<ProfileLoadOrderItemViewModel> moving) =>
        _dependencyManagementService.PlanRemoval(
            Mods.ToList(),
            ActiveProfileMods.Select(item => item.PackageId).ToArray(),
            moving.Select(item => item.PackageId).ToArray())
        .ImpactedDependents.ToArray();

    private void QueueDependencyRemovalConfirmation(
        IReadOnlyCollection<ProfileLoadOrderItemViewModel> moving,
        IReadOnlyCollection<DependencyReason> impacts)
    {
        var allPackageIds = moving.Select(item => item.PackageId)
            .Concat(impacts.Select(reason => reason.PackageId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var notificationId = "dependency-removal-" + Guid.NewGuid().ToString("N");
        _pendingDependencyRemovalNotificationId = notificationId;
        _pendingDependencyRemovalPackageIds = allPackageIds;

        var movedNames = string.Join(", ", moving.Select(item => item.DisplayName).Take(3));
        var impactNames = string.Join(", ", impacts.Select(reason => reason.DisplayName).Take(3));
        var suffix = impacts.Count > 3 ? $" and {impacts.Count - 3} more" : string.Empty;
        var message = $"Removing {movedNames} would leave {impactNames}{suffix} without a required dependency.";

        _notificationService.Enqueue(new NotificationRequest(
            "Dependent mods are still active",
            message,
            NotificationSeverity.Warning,
            [new NotificationAction("disable-impacted", "Disable All"), new NotificationAction("view-activity", "View Details")],
            TimeSpan.FromSeconds(18),
            notificationId));

        Append(message, ActivitySeverity.Warning);
        foreach (var impact in impacts)
            Append($"Removal impact: {impact.Explanation}", ActivitySeverity.Info);
    }

    private void DisablePendingDependencyRemoval(string notificationId)
    {
        if (!string.Equals(notificationId, _pendingDependencyRemovalNotificationId, StringComparison.OrdinalIgnoreCase)) return;
        var packageIds = _pendingDependencyRemovalPackageIds;
        _pendingDependencyRemovalNotificationId = null;
        _pendingDependencyRemovalPackageIds = Array.Empty<string>();
        var items = ActiveProfileMods
            .Where(item => packageIds.Contains(item.PackageId, StringComparer.OrdinalIgnoreCase))
            .Where(item => !item.IsMandatory)
            .ToArray();
        if (items.Length > 0)
            DisableModGroup(items, offerOrphanCleanup: true);
    }

    private void DisableModGroup(
        IReadOnlyCollection<ProfileLoadOrderItemViewModel> items,
        bool offerOrphanCleanup)
    {
        if (SelectedProfile is null || items.Count == 0) return;
        var moving = items.Where(ActiveProfileMods.Contains).Distinct().ToArray();
        if (moving.Length == 0) return;

        var before = CaptureLoadOrderUndoSnapshot();
        var automaticallyRemovedOrphans = Array.Empty<ProfileLoadOrderItemViewModel>();
        try
        {
            foreach (var item in moving) MoveToInactive(item);

            if (offerOrphanCleanup && OrphanCleanupPreference == OrphanCleanupMode.Automatic)
            {
                automaticallyRemovedOrphans = GetCurrentOrphanItems();
                foreach (var orphan in automaticallyRemovedOrphans) MoveToInactive(orphan);
            }

            SortInactiveMods();
            SyncCombinedLoadOrder();
        }
        catch (Exception ex)
        {
            RestoreLoadOrderUndoSnapshot(before);
            var failure = $"The {moving.Length}-mod disable operation failed and the previous load order was restored: {ex.Message}";
            Append(failure, ActivitySeverity.Error);
            _notificationService.Enqueue(new NotificationRequest(
                "Mod group disable failed",
                failure,
                NotificationSeverity.Error,
                [new NotificationAction("view-activity", "View Details")],
                TimeSpan.FromSeconds(12)));
            return;
        }

        var totalRemoved = moving.Length + automaticallyRemovedOrphans.Length;
        RegisterLoadOrderUndo(
            totalRemoved == 1 ? $"Disable {moving[0].DisplayName}" : $"Disable {totalRemoved} mods",
            before);

        var message = $"Removed {moving.Length} mod{(moving.Length == 1 ? string.Empty : "s")} from {SelectedProfile.Name}.";
        if (automaticallyRemovedOrphans.Length > 0)
            message += $" Automatically removed {automaticallyRemovedOrphans.Length} unused dependenc{(automaticallyRemovedOrphans.Length == 1 ? "y" : "ies")}.";
        Append(message, ActivitySeverity.Info);
        _notificationService.Enqueue(new NotificationRequest(
            automaticallyRemovedOrphans.Length > 0 ? "Mods and unused dependencies removed" : moving.Length == 1 ? "Mod disabled" : "Mod group disabled",
            message,
            NotificationSeverity.Information,
            [new NotificationAction("undo", "Undo"), new NotificationAction("view-activity", "View Details")],
            TimeSpan.FromSeconds(10)));

        ModSorterFeature.SelectItems(ModSorterFeature.InactiveList, moving);

        if (offerOrphanCleanup && OrphanCleanupPreference == OrphanCleanupMode.Ask)
            QueueOrphanCleanupSuggestion();
    }

    private void MoveToInactive(ProfileLoadOrderItemViewModel item)
    {
        ActiveProfileMods.Remove(item);
        item.IsEnabled = false;
        item.Position = 0;
        if (!InactiveInstalledMods.Contains(item)) InactiveInstalledMods.Add(item);
    }

    private ProfileLoadOrderItemViewModel[] GetCurrentOrphanItems()
    {
        var activeIds = ActiveProfileMods.Select(item => item.PackageId).ToArray();
        if (activeIds.Length == 0) return Array.Empty<ProfileLoadOrderItemViewModel>();
        return _dependencyManagementService.FindOrphans(Mods.ToList(), activeIds)
            .Where(reason => LoadOrderRules.CanDeactivate(reason.PackageId))
            .Select(reason => ActiveProfileMods.FirstOrDefault(item =>
                item.PackageId.Equals(reason.PackageId, StringComparison.OrdinalIgnoreCase)))
            .Where(item => item is not null)
            .Cast<ProfileLoadOrderItemViewModel>()
            .Distinct()
            .ToArray();
    }

    private void QueueOrphanCleanupSuggestion()
    {
        if (OrphanCleanupPreference == OrphanCleanupMode.Manual) return;
        var orphans = GetCurrentOrphanItems();
        if (orphans.Length == 0) return;

        var notificationId = "orphan-cleanup-" + Guid.NewGuid().ToString("N");
        _pendingOrphanCleanupNotificationId = notificationId;
        _pendingOrphanCleanupPackageIds = orphans.Select(item => item.PackageId).ToArray();
        var names = string.Join(", ", orphans.Select(item => item.DisplayName).Take(3));
        var suffix = orphans.Length > 3 ? $" and {orphans.Length - 3} more" : string.Empty;
        var message = $"{names}{suffix} are no longer required by any active mod.";

        _notificationService.Enqueue(new NotificationRequest(
            "Unused dependencies detected",
            message,
            NotificationSeverity.Information,
            [new NotificationAction("remove-orphans", "Remove Orphans"), new NotificationAction("view-activity", "View Details")],
            TimeSpan.FromSeconds(14),
            notificationId));
        Append(message, ActivitySeverity.Info);
    }

    private void RemovePendingOrphans(string notificationId)
    {
        if (!string.Equals(notificationId, _pendingOrphanCleanupNotificationId, StringComparison.OrdinalIgnoreCase)) return;
        var packageIds = _pendingOrphanCleanupPackageIds;
        _pendingOrphanCleanupNotificationId = null;
        _pendingOrphanCleanupPackageIds = Array.Empty<string>();
        var items = ActiveProfileMods
            .Where(item => packageIds.Contains(item.PackageId, StringComparer.OrdinalIgnoreCase))
            .Where(item => !item.IsMandatory)
            .ToArray();
        if (items.Length > 0)
            DisableModGroup(items, offerOrphanCleanup: false);
    }

    private void SortInactiveMods()
    {
        var sorted = InactiveInstalledMods.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        InactiveInstalledMods.Clear();
        foreach (var item in sorted) InactiveInstalledMods.Add(item);
    }

    private void SyncCombinedLoadOrder(bool normalizeActiveOrder = true)
    {
        if (normalizeActiveOrder) NormalizeActiveLoadOrder();
        ProfileLoadOrderItems.Clear();
        var position = 1;
        foreach (var item in ActiveProfileMods)
        {
            item.IsEnabled = true;
            item.Position = position++;
            ProfileLoadOrderItems.Add(item);
        }
        foreach (var item in InactiveInstalledMods)
        {
            item.IsEnabled = false;
            item.Position = 0;
            ProfileLoadOrderItems.Add(item);
        }
        IsLoadOrderDirty = true;
        Notify(nameof(IsLoadOrderCanonical));
    }


    public bool IsLoadOrderCanonical
    {
        get
        {
            var currentOrder = ActiveProfileMods
                .Select(item => item.PackageId)
                .ToArray();

            var canonicalOrder = CalculateCanonicalLoadOrder();

            return canonicalOrder.SequenceEqual(
                currentOrder,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    private void NormalizeActiveLoadOrder()
    {
        var byId = ActiveProfileMods
            .GroupBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var normalizedIds = LoadOrderRules.Normalize(ActiveProfileMods.Select(item => item.PackageId));
        if (normalizedIds.SequenceEqual(ActiveProfileMods.Select(item => item.PackageId), StringComparer.OrdinalIgnoreCase)) return;

        ActiveProfileMods.Clear();
        foreach (var packageId in normalizedIds)
            if (byId.TryGetValue(packageId, out var item)) ActiveProfileMods.Add(item);
    }

    private void ShowProfileIssues_Click(object sender, RoutedEventArgs e)
    {
        ShowIssuesOnly = true;
        ShowFullLibrary = false;
        ShowPage(IssueViewerWorkspacePanel, "Issue Viewer");
    }

    private void FixProfileIssues_Click(object sender, RoutedEventArgs e)
    {
        AutoSortLoadOrder_Click(sender, e);
        Append("Applied available safe load-order fixes as an unsaved preview.", ActivitySeverity.Info);
    }

    private void LoadOrderList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _loadOrderDragStart = e.GetPosition(null);

    private void LoadOrderList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || sender is not ListBox list) return;
        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _loadOrderDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _loadOrderDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        if (list.SelectedItem is ProfileLoadOrderItemViewModel item && !item.IsLoadOrderAnchor)
            DragDrop.DoDragDrop(list, item, DragDropEffects.Move);
    }

    private void LoadOrderList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ProfileLoadOrderItemViewModel)) ||
            e.Data.GetData(typeof(ProfileLoadOrderItemViewModel)) is not ProfileLoadOrderItemViewModel source ||
            sender is not ListBox list) return;
        var targetElement = list.InputHitTest(e.GetPosition(list)) as DependencyObject;
        while (targetElement is not null && targetElement is not ListBoxItem)
            targetElement = VisualTreeHelper.GetParent(targetElement);
        var target = (targetElement as ListBoxItem)?.DataContext as ProfileLoadOrderItemViewModel;
        if (source.IsLoadOrderAnchor || target is null || ReferenceEquals(source, target) || target.IsLoadOrderAnchor)
        {
            if (source.IsLoadOrderAnchor || target?.IsLoadOrderAnchor == true)
                Append("Canonical load-order anchors cannot be moved or used as manual insertion targets.", ActivitySeverity.Warning);
            return;
        }
        var before = CaptureLoadOrderUndoSnapshot();
        var oldIndex = ProfileLoadOrderItems.IndexOf(source);
        var newIndex = ProfileLoadOrderItems.IndexOf(target);
        if (oldIndex < 0 || newIndex < 1) return;
        ProfileLoadOrderItems.Move(oldIndex, newIndex);
        RenumberLoadOrder();
        IsLoadOrderDirty = true;
        RegisterLoadOrderUndo($"Reorder {source.DisplayName}", before);
    }

    private void RenumberLoadOrder()
    {
        var position = 1;
        foreach (var item in ActiveProfileMods) item.Position = position++;
    }

    private void OfficialDlcToggle_Click(object sender, RoutedEventArgs e)
    {
        var before = CaptureLoadOrderUndoSnapshot();
        if (sender is CheckBox checkBox && checkBox.DataContext is ProfileLoadOrderItemViewModel item && !item.IsEnabled)
        {
            ActiveProfileMods.Remove(item);
            item.Position = 0;
            InactiveInstalledMods.Add(item);
            SortInactiveMods();
        }
        SyncCombinedLoadOrder();
        RegisterLoadOrderUndo("Toggle official DLC", before);
    }

    private async void SaveLoadOrder_Click(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Save Load Order", SaveLoadOrderAsync);

    private async Task SaveLoadOrderAsync()
    {
        if (SelectedProfile is null || !IsLoadOrderDirty) return;
        var profile = SelectedProfile;
        var activeMods = ActiveProfileMods.Select(item => item.PackageId).ToArray();
        var result = await RunFeatureTaskAsync(
            "profile.save-load-order",
            "Save Active Mod Load Order",
            context =>
            {
                context.Report(new BackgroundTaskProgress(
                    "Saving active mod load order",
                    $"Writing {activeMods.Length} active mod(s) to '{profile.Name}'.",
                    profile.ModsConfigPath,
                    null,
                    0,
                    activeMods.Length,
                    "Persisting the canonical profile load order",
                    profile.ModsConfigPath));
                return _profileWorkspaceService.SaveLoadOrderAsync(profile, activeMods, context.CancellationToken);
            });
        Append(result.Message, result.Success ? ActivitySeverity.Success : ActivitySeverity.Error);
        if (!result.Success || result.UpdatedProfile is null)
        {
            _notificationService.Enqueue(new NotificationRequest(
                "Load order save failed",
                result.Message,
                NotificationSeverity.Error,
                [new NotificationAction("view-activity", "View Log")],
                TimeSpan.FromSeconds(12)));
            return;
        }
        var index = Profiles.IndexOf(SelectedProfile);
        if (index >= 0) Profiles[index] = result.UpdatedProfile;
        SelectedProfile = result.UpdatedProfile;
        IsLoadOrderDirty = false;
        _undoService.Clear();
        _notificationService.Enqueue(new NotificationRequest(
            "Load order saved",
            $"Saved {ActiveProfileMods.Count} active mods to {SelectedProfile.Name}.",
            NotificationSeverity.Success));
    }

    private void RevertLoadOrder_Click(object sender, RoutedEventArgs e)
    {
        RebuildProfileLoadOrder();
        _undoService.Clear();
        _notificationService.Enqueue(new NotificationRequest(
            "Changes reverted",
            "The working load order was restored from the saved profile.",
            NotificationSeverity.Information));
    }

    private void AutoSortLoadOrder_Click(object sender, RoutedEventArgs e) => ApplyOptimizedLoadOrder(true);

    private IReadOnlyList<string> CalculateCanonicalLoadOrder()
    {
        var current = ActiveProfileMods.Select(item => item.PackageId).ToList();
        if (_analysisSnapshot is null) return LoadOrderRules.Normalize(current);
        var rank = _analysisSnapshot.ProposedOrder.OrderedPackageIds
            .Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index, StringComparer.OrdinalIgnoreCase);
        var currentSet = current.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependencySortedBody = current
            .Where(id => !LoadOrderRules.IsAnchor(id))
            .Select((id, index) => (id, index))
            .OrderBy(pair => rank.GetValueOrDefault(pair.id, int.MaxValue))
            .ThenBy(pair => pair.index)
            .Select(pair => pair.id);
        return LoadOrderRules.Normalize(
            LoadOrderRules.TopAnchors.Where(currentSet.Contains)
                .Concat(dependencySortedBody)
                .Concat(LoadOrderRules.BottomAnchors.Where(currentSet.Contains)));
    }

    private bool ApplyOptimizedLoadOrder(bool announce, bool registerUndo = true)
    {
        if (SelectedProfile is null) return false;
        var before = registerUndo ? CaptureLoadOrderUndoSnapshot() : null;
        var current = ActiveProfileMods.Select(item => item.PackageId).ToList();
        var sorted = CalculateCanonicalLoadOrder();
        if (sorted.SequenceEqual(current, StringComparer.OrdinalIgnoreCase))
        {
            if (announce)
            {
                const string message = "The selected profile already matches RimForge's optimized dependency order.";
                Append(message, ActivitySeverity.Success);
                _notificationService.Enqueue(new NotificationRequest(
                    "Load order already optimized",
                    message,
                    NotificationSeverity.Information));
            }
            return false;
        }
        // Preserve deterministic behavior when a profile contains duplicate package IDs.
        // The duplicate remains an analysis finding; auto-sort must not crash while the
        // user is trying to resolve it.
        var byId = ActiveProfileMods
            .GroupBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        ActiveProfileMods.Clear();
        foreach (var id in sorted)
            if (byId.TryGetValue(id, out var item)) ActiveProfileMods.Add(item);
        SyncCombinedLoadOrder();
        if (before is not null) RegisterLoadOrderUndo("Auto-sort load order", before);
        if (announce)
        {
            const string message = "Applied RimForge's optimized load-order preview. Save to persist the changes.";
            Append(message, ActivitySeverity.Success);
            _notificationService.Enqueue(new NotificationRequest(
                "Load order optimized",
                message,
                NotificationSeverity.Success,
                [new NotificationAction("undo", "Undo")],
                TimeSpan.FromSeconds(10)));
        }
        return true;
    }

}
