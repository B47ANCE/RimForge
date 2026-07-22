using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RimForge.App.Features.ForgeView;
using RimForge.App.Features.ModInspector;
using RimForge.Analysis.Models;
using RimForge.Core.Models;
using RimForge.UI.ViewModels;

namespace RimForge.App;

public partial class MainWindow
{
    public bool CanNavigateSelectionBack => GetActiveSelectionIndex() > 0;
    public bool CanNavigateSelectionForward
    {
        get
        {
            var collection = GetActiveSelectionCollection();
            var index = GetActiveSelectionIndex(collection);
            return index >= 0 && index < collection.Count - 1;
        }
    }
    public string SelectionBreadcrumb => SelectedMod is null
        ? "No mod selected"
        : $"{PageTitle}  ›  {SelectedMod.DisplayName}";

    public IReadOnlyList<ModRecord> SelectedDependencyMods => SelectedMod is null
        ? Array.Empty<ModRecord>()
        : SelectedMod.Dependencies
            .Select(dependency => FindModByPackageId(dependency.PackageId))
            .Where(mod => mod is not null)
            .Cast<ModRecord>()
            .DistinctBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(mod => mod.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<ModRecord> SelectedDependentMods => SelectedMod?.PackageId is not { Length: > 0 } packageId
        ? Array.Empty<ModRecord>()
        : Mods.Where(mod => mod.Dependencies.Any(dependency =>
                string.Equals(dependency.PackageId, packageId, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(mod => mod.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();


    public DependencyIntelligenceReport SelectedDependencyIntelligence => _dependencyIntelligence;
    public string SelectedDependencySummary => _dependencyIntelligence.Summary;
    public string SelectedWhyEnabledText => _dependencyIntelligence.WhyEnabled;
    public string SelectedRemovalImpactText => _dependencyIntelligence.RemovalExplanation;
    public string SelectedDependencyConfidenceText => $"{_dependencyIntelligence.ConfidencePercent}% confidence";
    public bool SelectedIsOrphanCandidate => _dependencyIntelligence.IsOrphan;
    public IReadOnlyList<DependencyReason> SelectedRemovalImpactMods => _dependencyIntelligence.RemovalImpact;
    public DependencyManagementSummary CurrentDependencyManagementSummary => _dependencyManagementService.Summarize(
        Mods.ToList(), ActiveProfileMods.Select(item => item.PackageId).ToArray());
    public string DependencyManagementHealthText => CurrentDependencyManagementSummary.Summary;
    public string DependencyManagementCountsText =>
        $"{CurrentDependencyManagementSummary.ActiveModCount} active · {CurrentDependencyManagementSummary.DependencyBearingModCount} dependency-bearing · {CurrentDependencyManagementSummary.MissingDependencyCount} missing";

    private void RefreshDependencyIntelligence()
    {
        var activeIds = ActiveProfileMods.Select(item => item.PackageId).ToArray();
        _dependencyIntelligence = _dependencyIntelligenceService.Analyze(Mods.ToList(), activeIds, SelectedMod?.PackageId);
        Notify(nameof(SelectedDependencyIntelligence));
        Notify(nameof(SelectedDependencySummary));
        Notify(nameof(SelectedWhyEnabledText));
        Notify(nameof(SelectedRemovalImpactText));
        Notify(nameof(SelectedDependencyConfidenceText));
        Notify(nameof(SelectedIsOrphanCandidate));
        Notify(nameof(SelectedRemovalImpactMods));
        NotifyDependencyManagementSummary();
    }

    private void NotifyDependencyManagementSummary()
    {
        Notify(nameof(CurrentDependencyManagementSummary));
        Notify(nameof(DependencyManagementHealthText));
        Notify(nameof(DependencyManagementCountsText));
    }

    private void RecordSelection(ModRecord? mod)
    {
        _navigationContext.Record(mod?.PackageId);
        NotifySelectionNavigation();
    }

    private void NavigationContext_NavigationChanged(object? sender, EventArgs e) =>
        NotifySelectionNavigation();

    private ModRecord? FindModByPackageId(string? packageId) => string.IsNullOrWhiteSpace(packageId)
        ? null
        : Mods.FirstOrDefault(mod => string.Equals(mod.PackageId, packageId, StringComparison.OrdinalIgnoreCase));

    private void SelectModByPackageId(string? packageId)
        => SelectModByPackageId(packageId, ForgeGraphQueryOrigin.Inspector);

    private void SelectModByPackageId(string? packageId, ForgeGraphQueryOrigin origin)
    {
        var mod = FindModByPackageId(packageId);
        if (mod is null) return;
        SelectMod(mod, origin);
        SelectedSorterItem = ModSorterItems.FirstOrDefault(item => item.Mod.Id == mod.Id);
    }

    private void NotifySelectionNavigation()
    {
        Notify(nameof(CanNavigateSelectionBack));
        Notify(nameof(CanNavigateSelectionForward));
        Notify(nameof(SelectionBreadcrumb));
        Notify(nameof(SelectedDependencyMods));
        Notify(nameof(SelectedDependentMods));
    }

    private void SelectionBackRequested(object sender, RoutedEventArgs e) => NavigateActiveCollection(-1);
    private void SelectionForwardRequested(object sender, RoutedEventArgs e) => NavigateActiveCollection(1);

    private IReadOnlyList<ModRecord> GetActiveSelectionCollection()
    {
        IEnumerable<ModRecord> candidates;
        if (IsSearchActive)
        {
            candidates = ModsView.Cast<ModRecord>();
        }
        else if (string.Equals(PageTitle, "Issue Viewer", StringComparison.OrdinalIgnoreCase))
        {
            candidates = IssueItemsView.Cast<IssueWorkItem>()
                .Select(issue => FindModByPackageId(issue.PackageId))
                .Where(mod => mod is not null)
                .Cast<ModRecord>();
        }
        else if (string.Equals(PageTitle, "Mod Sorter", StringComparison.OrdinalIgnoreCase))
        {
            candidates = ActiveProfileModsView.Cast<ProfileLoadOrderItemViewModel>()
                .Concat(InactiveInstalledModsView.Cast<ProfileLoadOrderItemViewModel>())
                .Select(item => item.Mod)
                .Where(mod => mod is not null)
                .Cast<ModRecord>();
        }
        else
        {
            candidates = ModsView.Cast<ModRecord>();
        }

        return candidates
            .DistinctBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private int GetActiveSelectionIndex() => GetActiveSelectionIndex(GetActiveSelectionCollection());

    private int GetActiveSelectionIndex(IReadOnlyList<ModRecord> collection)
    {
        if (SelectedMod is null) return -1;
        for (var index = 0; index < collection.Count; index++)
            if (string.Equals(collection[index].Id, SelectedMod.Id, StringComparison.OrdinalIgnoreCase))
                return index;
        return -1;
    }

    private void NavigateActiveCollection(int offset)
    {
        var collection = GetActiveSelectionCollection();
        if (collection.Count == 0) return;
        var index = GetActiveSelectionIndex(collection);
        var target = index < 0 ? 0 : index + offset;
        if (target < 0 || target >= collection.Count) return;
        SelectedMod = collection[target];
        NotifySelectionNavigation();
    }

    private void ModInspector_RelatedModRequested(object? sender, RelatedModRequestedEventArgs e) =>
        SelectModByPackageId(e.PackageId);

    private void ForgeView_ModNavigationRequested(object? sender, ModNavigationRequestedEventArgs e) =>
        SelectModByPackageId(e.PackageId, e.Origin);
}
