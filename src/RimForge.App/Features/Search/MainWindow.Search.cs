using System;
using System.Linq;
using System.Collections.ObjectModel;
using RimForge.App.Features.Search;
using RimForge.Analysis.Models;
using RimForge.Core.Models;
using RimForge.UI.Presentation;
using RimForge.UI.ViewModels;

namespace RimForge.App;

public partial class MainWindow
{
    private void RefreshSearchAwareViews()
    {
        ModsView.Refresh();
        ModSorterView.Refresh();
        ActiveProfileModsView.Refresh();
        InactiveInstalledModsView.Refresh();
        IssueItemsView.Refresh();
        DependencyEdgesView.Refresh();
        RefreshSearchDiscoveryResults();
        NotifySearchResultState();
    }

    private void NotifySearchResultState()
    {
        Notify(nameof(IsSearchActive));
        Notify(nameof(ActiveProfileFilteredCount));
        Notify(nameof(InactiveInstalledFilteredCount));
        Notify(nameof(IssueFilteredCount));
        Notify(nameof(ForgeSearchMatchCount));
        Notify(nameof(ActiveProfileCountText));
        Notify(nameof(InactiveInstalledCountText));
        Notify(nameof(IssueSearchSummaryText));
        Notify(nameof(SearchSummaryText));
        Notify(nameof(SearchValidationText));
        Notify(nameof(HasSearchValidationError));
        Notify(nameof(SearchSuggestions));
        Notify(nameof(HasSearchSuggestions));
        Notify(nameof(SearchSuggestionText));
        Notify(nameof(ActiveSearchChips));
        Notify(nameof(HasActiveSearchChips));
        Notify(nameof(ActiveSearchChipText));
        Notify(nameof(SearchDiscoveryResults));
        Notify(nameof(HasSearchDiscoveryResults));
        Notify(nameof(IsSearchFlyoutOpen));
        Notify(nameof(HasNoSearchDiscoveryResults));
        Notify(nameof(SearchFlyoutMessage));
        Notify(nameof(SearchMatchedPackageIds));
        Notify(nameof(SelectedSearchDiscoveryResult));
    }

    private StructuredSearchQuery CurrentSearchQuery => StructuredSearchQuery.Parse(SearchText);
    public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);
    public bool IsSearchFlyoutOpen => IsSearchActive;
    public bool HasNoSearchDiscoveryResults => IsSearchFlyoutOpen && !HasSearchDiscoveryResults;
    public string SearchFlyoutMessage => HasSearchValidationError
        ? SearchValidationText
        : $"No mods, issues, or RimForge features match “{SearchText.Trim()}”.";
    public string SearchPromptText => _activeWorkspaceDestination switch
    {
        "Issue Viewer" => "Search issues · e.g. issue:dependency, mod:combat, active:true",
        "ForgeView" => "Search the dependency graph · e.g. combat, requires:harmony, source:workshop",
        "Texture Tools" => "Search textures, DDS mods, or open Texture Conversion Tools",
        "Settings" => "Search settings or features · e.g. profiles, paths, launch",
        "Console" => "Search activity, game logs, mods, or RimForge features",
        _ => "Search visible mods · e.g. combat, author:ludeon, source:workshop, active:true"
    };
    public bool HasSearchValidationError => CurrentSearchQuery.Errors.Count > 0;
    public string SearchValidationText => HasSearchValidationError
        ? string.Join(" ", CurrentSearchQuery.Errors)
        : string.Empty;
    public IReadOnlyList<string> SearchSuggestions => StructuredSearchQuery.GetSuggestions(SearchText);
    public bool HasSearchSuggestions => SearchSuggestions.Count > 0 && !HasSearchValidationError;
    public string SearchSuggestionText => HasSearchSuggestions
        ? "Suggestions: " + string.Join("  ·  ", SearchSuggestions)
        : string.Empty;
    public IReadOnlyList<string> ActiveSearchChips => CurrentSearchQuery.Chips;
    public bool HasActiveSearchChips => ActiveSearchChips.Count > 0;
    public string ActiveSearchChipText => string.Join("   ", ActiveSearchChips.Select(chip => $"[{chip}]"));
    public int ActiveProfileFilteredCount => ActiveProfileModsView.Cast<object>().Count();
    public int InactiveInstalledFilteredCount => InactiveInstalledModsView.Cast<object>().Count();
    public int IssueFilteredCount => IssueItemsView.Cast<object>().Count();
    public int ForgeSearchMatchCount => DependencyEdgesView.Cast<object>().Count();
    public IReadOnlyList<string> SearchMatchedPackageIds
    {
        get
        {
            if (!IsSearchActive || HasSearchValidationError) return Array.Empty<string>();
            var activeIds = SelectedProfile?.ActiveMods.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var criteria = new ModFilterCriteria(SearchText, true, false, activeIds, Mods.ToList(), CurrentSearchQuery);
            return Mods
                .Where(mod => _modFilteringService.Matches(mod, criteria))
                .Select(mod => mod.PackageId)
                .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public string ActiveProfileCountText => IsSearchActive
        ? $"{ActiveProfileFilteredCount} of {ActiveProfileMods.Count} active"
        : $"{ActiveProfileMods.Count} active";

    public string InactiveInstalledCountText => IsSearchActive
        ? $"{InactiveInstalledFilteredCount} of {InactiveInstalledMods.Count} available"
        : $"{InactiveInstalledMods.Count} available";

    public string IssueSearchSummaryText => IsSearchActive
        ? $"{IssueFilteredCount} matching issue{(IssueFilteredCount == 1 ? string.Empty : "s")}"
        : string.Empty;

    public string SearchSummaryText => IsSearchActive
        ? $"{ActiveProfileFilteredCount + InactiveInstalledFilteredCount} mods · {IssueFilteredCount} issues · {ForgeSearchMatchCount} relationships"
        : string.Empty;

    private bool FilterProfileLoadOrderItem(object item)
    {
        if (item is not ProfileLoadOrderItemViewModel loadOrderItem) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        if (loadOrderItem.Mod is not null)
        {
            var criteria = new ModFilterCriteria(SearchText, true, false, SelectedProfile?.ActiveMods.ToHashSet(StringComparer.OrdinalIgnoreCase), Mods.ToList(), CurrentSearchQuery);
            return _modFilteringService.Matches(loadOrderItem.Mod, criteria);
        }

        var query = CurrentSearchQuery;
        if (!query.IsValid) return false;
        return query.Evaluate(clause => clause.Field switch
        {
            "identity" or "mod" => Contains(loadOrderItem.DisplayName, clause.Value) || Contains(loadOrderItem.PackageId, clause.Value),
            "package" => Contains(loadOrderItem.PackageId, clause.Value),
            "source" => Contains(loadOrderItem.SourceLabel, clause.Value),
            "active" => clause.Value.Equals("true", StringComparison.OrdinalIgnoreCase)
                        || clause.Value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                        || clause.Value == "1",
            _ => false
        });
    }

    private bool FilterIssueItem(object item)
    {
        if (item is not IssueWorkItem issue) return false;
        var query = CurrentSearchQuery;
        if (!query.IsValid) return false;
        if (query.IsEmpty) return true;

        return query.Evaluate(clause => clause.Field switch
        {
            "identity" or "mod" => Contains(issue.ModName, clause.Value) || Contains(issue.PackageId, clause.Value),
            "package" => Contains(issue.PackageId, clause.Value),
            "issue" => Contains(issue.Category, clause.Value)
                       || Contains(issue.Title, clause.Value)
                       || Contains(issue.Explanation, clause.Value)
                       || Contains(issue.RecommendedAction, clause.Value),
            "active" => SelectedProfile?.ActiveMods.Contains(issue.PackageId, StringComparer.OrdinalIgnoreCase) == ParseSearchBoolean(clause.Value),
            _ => IssueRelatedModMatches(issue, clause)
        });
    }

    private bool IssueRelatedModMatches(IssueWorkItem issue, SearchClause clause)
    {
        var mod = Mods.FirstOrDefault(candidate => string.Equals(candidate.PackageId, issue.PackageId, StringComparison.OrdinalIgnoreCase));
        if (mod is null) return false;
        var criteria = new ModFilterCriteria(SearchText, true, false, SelectedProfile?.ActiveMods.ToHashSet(StringComparer.OrdinalIgnoreCase), Mods.ToList(), new StructuredSearchQuery([clause], Array.Empty<string>()));
        return _modFilteringService.Matches(mod, criteria);
    }

    private static bool ParseSearchBoolean(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || value == "1";


    public ObservableCollection<SearchDiscoveryResult> SearchDiscoveryResults { get; } = new();

    private SearchDiscoveryResult? _selectedSearchDiscoveryResult;
    public SearchDiscoveryResult? SelectedSearchDiscoveryResult
    {
        get => _selectedSearchDiscoveryResult;
        set => Set(ref _selectedSearchDiscoveryResult, value);
    }

    public bool HasSearchDiscoveryResults => IsSearchActive
        && !HasSearchValidationError
        && SearchDiscoveryResults.Count > 0;

    private void RefreshSearchDiscoveryResults()
    {
        SearchDiscoveryResults.Clear();
        SelectedSearchDiscoveryResult = null;
        if (!IsSearchActive || HasSearchValidationError) return;

        var query = CurrentSearchQuery;
        var activeIds = SelectedProfile?.ActiveMods.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var criteria = new ModFilterCriteria(SearchText, true, false, activeIds, Mods.ToList(), query);

        var results = new List<SearchDiscoveryResult>();
        foreach (var mod in Mods.Where(mod => _modFilteringService.Matches(mod, criteria)))
        {
            var score = ScoreSearchMatch(SearchText, mod.DisplayName, mod.PackageId, mod.Author, mod.WorkshopId);
            var state = activeIds?.Contains(mod.PackageId ?? string.Empty) == true ? "Active" : "Inactive";
            var presentation = ActiveProfileMods
                .Concat(InactiveInstalledMods)
                .FirstOrDefault(item => string.Equals(item.PackageId, mod.PackageId, StringComparison.OrdinalIgnoreCase));
            var source = presentation?.Source ?? mod.Source;
            var healthState = presentation?.HealthState ?? (mod.Errors.Count > 0 ? "Critical" : "Healthy");
            var healthToolTip = presentation?.HealthToolTip ?? (mod.Errors.Count > 0
                ? $"{mod.Errors.Count} finding(s) — open this mod for details."
                : "Healthy — open this mod in the Inspector.");
            results.Add(new SearchDiscoveryResult(
                SearchDiscoveryKind.Mod,
                mod.DisplayName,
                $"{state} · {mod.DisplayPackageId} · {ModSourcePresentation.GetDisplayName(source)}",
                mod.Id,
                "\uE8A5",
                score + 100,
                ModSourcePresentation.GetIconGeometry(source),
                ModSourcePresentation.GetToolTip(source),
                healthState,
                healthToolTip));
        }

        foreach (var issue in IssueItems.Where(issue => FilterIssueItem(issue)))
        {
            var score = ScoreSearchMatch(SearchText, issue.Title, issue.ModName, issue.PackageId, issue.Category);
            results.Add(new SearchDiscoveryResult(
                SearchDiscoveryKind.Issue,
                issue.Title,
                $"{issue.ModName} · {issue.Category}",
                issue.Id,
                "\uE7BA",
                score + 60));
        }

        foreach (var workspace in SearchableWorkspaces)
        {
            var featureCandidates = new[] { workspace.Title }.Concat(workspace.Aliases);
            if (!MatchesSearchFeature(SearchText, featureCandidates)) continue;
            results.Add(new SearchDiscoveryResult(
                SearchDiscoveryKind.Workspace,
                workspace.Title,
                $"RimForge feature · {workspace.Description}",
                workspace.Destination,
                workspace.Glyph,
                1000 + ScoreSearchMatch(SearchText, featureCandidates.ToArray())));
        }

        foreach (var result in results
                     .OrderByDescending(result => result.Score)
                     .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
                     .Take(16))
            SearchDiscoveryResults.Add(result);

        SelectedSearchDiscoveryResult = SearchDiscoveryResults.FirstOrDefault();
        SelectUniqueModSearchResult();
    }

    private static readonly (string Title, string Destination, string Glyph, string Description, string[] Aliases)[] SearchableWorkspaces =
    [
        ("Mod Sorter", "Mod Sorter", "\uE8CB", "Manage active and inactive mods", ["mods", "load order", "sorting", "dashboard"]),
        ("Issue Viewer", "Issue Viewer", "\uE7BA", "Review health findings and repairs", ["issues", "diagnostics", "health", "anvil", "repairs"]),
        ("ForgeView", "ForgeView", "\uE9D2", "Explore dependency and incompatibility relationships", ["graph", "dependencies", "relationships", "conflicts"]),
        ("Texture Conversion Tools", "Texture Tools", "\uE790", "Analyze and convert textures to BC7 DDS", ["texture", "textures", "dds", "bc7", "converter"]),
        ("Console", "Console", "\uE756", "Inspect RimForge activity and game logs", ["log", "logs", "activity", "player.log"]),
        ("Settings", "Settings", "\uE713", "Configure profiles, paths, launch, and behavior", ["preferences", "profiles", "paths", "launch", "configuration"])
    ];

    private static bool MatchesSearchFeature(string query, IEnumerable<string> candidates)
    {
        if (query.Contains(':') || query.Contains('>') || query.Contains('<')) return false;
        var searchable = candidates.ToArray();
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.Length > 0 && terms.All(term =>
            searchable.Any(candidate => candidate.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private void SelectUniqueModSearchResult()
    {
        if (SearchDiscoveryResults.Count != 1 || SearchDiscoveryResults[0].Kind != SearchDiscoveryKind.Mod) return;
        var result = SearchDiscoveryResults[0];
        var mod = Mods.FirstOrDefault(candidate => candidate.Id == result.TargetId);
        if (mod is null) return;
        if (!ReferenceEquals(SelectedMod, mod)) SelectedMod = mod;
        ForgeViewFeature.SynchronizeSelection(mod.PackageId, ForgeGraphQueryOrigin.Search);
        SelectedSorterItem = ModSorterItems.FirstOrDefault(item => item.Mod.Id == mod.Id);
        var activeItem = ActiveProfileMods.FirstOrDefault(item => string.Equals(item.PackageId, mod.PackageId, StringComparison.OrdinalIgnoreCase));
        if (activeItem is not null)
        {
            SelectedLoadOrderItem = activeItem;
            ModSorterFeature.SelectItems(ModSorterFeature.ActiveList, [activeItem]);
            return;
        }

        var inactiveItem = InactiveInstalledMods.FirstOrDefault(item => string.Equals(item.PackageId, mod.PackageId, StringComparison.OrdinalIgnoreCase));
        if (inactiveItem is not null)
            ModSorterFeature.SelectItems(ModSorterFeature.InactiveList, [inactiveItem]);
    }

    private static int ScoreSearchMatch(string query, params string?[] candidates)
    {
        var normalized = query.Trim();
        var score = 0;
        foreach (var candidate in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
        {
            if (candidate!.Equals(normalized, StringComparison.OrdinalIgnoreCase)) score = Math.Max(score, 100);
            else if (candidate.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)) score = Math.Max(score, 70);
            else if (candidate.Contains(normalized, StringComparison.OrdinalIgnoreCase)) score = Math.Max(score, 40);
        }
        return score;
    }

    private void CommandBar_SearchResultInvoked(object? sender, SearchDiscoveryResult result)
    {
        switch (result.Kind)
        {
            case SearchDiscoveryKind.Mod:
            {
                var mod = Mods.FirstOrDefault(candidate => candidate.Id == result.TargetId);
                if (mod is null) return;
                SelectedMod = mod;
                ForgeViewFeature.SynchronizeSelection(mod.PackageId, ForgeGraphQueryOrigin.Search);
                SelectedSorterItem = ModSorterItems.FirstOrDefault(item => item.Mod.Id == mod.Id);
                ScrollToWorkspaceSection(IssueViewerWorkspacePanel, "Mod Sorter");
                break;
            }
            case SearchDiscoveryKind.Issue:
            {
                SelectedIssueItem = IssueItems.FirstOrDefault(issue =>
                    string.Equals(issue.Id, result.TargetId, StringComparison.OrdinalIgnoreCase));
                ScrollToWorkspaceSection(IssueViewerWorkspacePanel, "Issue Viewer");
                break;
            }
            case SearchDiscoveryKind.Workspace:
                NavigateToSearchWorkspace(result.TargetId);
                break;
        }

        RecordGlobalNavigationSnapshot();
    }

    private void NavigateToSearchWorkspace(string destination)
    {
        System.Windows.FrameworkElement section = destination switch
        {
            "ForgeView" => ForgeViewPanel,
            "Texture Tools" => TextureToolsPanel,
            "Settings" => SettingsPanel,
            "Console" => ConsolePanel,
            "Issue Viewer" => IssueViewerWorkspacePanel,
            "Mod Sorter" => ModSorterWorkspacePanel,
            _ => DashboardPanel
        };
        ScrollToWorkspaceSection(section, destination);
    }

}
