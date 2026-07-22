using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Media;
using RimForge.Core.Models;
using RimForge.UI.Presentation;

namespace RimForge.UI.ViewModels;

public sealed class ModSorterItemViewModel : INotifyPropertyChanged
{
    public ModSorterItemViewModel(
        ModRecord mod,
        bool isActive,
        int? loadOrder,
        int analysisIssueCount = 0,
        int directDependentCount = 0,
        int transitiveDependentCount = 0,
        bool isInCycle = false,
        string? analysisHealthLabel = null,
        string? impactLabel = null,
        int? proposedLoadOrder = null,
        string? sortReason = null,
        string? sortRuleSource = null,
        LoadOrderRuleConfidence sortConfidence = LoadOrderRuleConfidence.Experimental,
        bool isSortRequired = false)
    {
        Mod = mod;
        IsActive = isActive;
        LoadOrder = loadOrder;
        AnalysisIssueCount = analysisIssueCount;
        DirectDependentCount = directDependentCount;
        TransitiveDependentCount = transitiveDependentCount;
        IsInCycle = isInCycle;
        AnalysisHealthLabel = analysisHealthLabel ?? (mod.Errors.Count == 0 ? "HEALTHY" : $"{mod.Errors.Count} ISSUE(S)");
        ImpactLabel = impactLabel ?? "No dependents";
        ProposedLoadOrder = proposedLoadOrder;
        SortReason = sortReason ?? "Run Forge to calculate an explainable load-order recommendation.";
        SortRuleSource = sortRuleSource ?? "Not analyzed";
        SortConfidence = sortConfidence;
        IsSortRequired = isSortRequired;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ModRecord Mod { get; }
    public bool IsActive { get; }
    public int? LoadOrder { get; }
    public int AnalysisIssueCount { get; }
    public int DirectDependentCount { get; }
    public int TransitiveDependentCount { get; }
    public bool IsInCycle { get; }
    public string AnalysisHealthLabel { get; }
    public string ImpactLabel { get; }
    public int? ProposedLoadOrder { get; }
    public string SortReason { get; }
    public string SortRuleSource { get; }
    public LoadOrderRuleConfidence SortConfidence { get; }
    public bool IsSortRequired { get; }
    public bool HasSortDecision => ProposedLoadOrder is not null;
    public bool WillMove => LoadOrder is not null && ProposedLoadOrder is not null && LoadOrder != ProposedLoadOrder;
    public string ProposedLoadOrderText => ProposedLoadOrder is null ? "—" : (ProposedLoadOrder.Value + 1).ToString();
    public string SortDecisionLabel => !HasSortDecision
        ? "Not analyzed"
        : IsSortRequired
            ? $"Required · {SortConfidence}"
            : $"Recommended · {SortConfidence}";
    public string SortMovementText => !HasSortDecision
        ? "No proposed position"
        : LoadOrder is null
            ? $"Proposed position {ProposedLoadOrderText}"
            : WillMove
                ? $"Move from {LoadOrderText} to {ProposedLoadOrderText}"
                : $"Position {LoadOrderText} is already valid";
    public string LoadOrderText => LoadOrder is null ? "—" : (LoadOrder.Value + 1).ToString();
    public string ActiveLabel => IsActive ? "ACTIVE" : "LIBRARY";
    public string StateStatusLabel => $"{(IsActive ? "Active" : "Inactive")} & {FormatStatusLabel(AnalysisHealthLabel)}";
    public ModSource Source => Mod.Source;
    public bool IsOfficialContent => Source == ModSource.Official;
    public bool ShowEvidenceBadges => !IsOfficialContent && Mod.Evidence.Badges.Count > 0;
    public string SourceLabel => ModSourcePresentation.GetShortLabel(Source);
    public Geometry SourceIconGeometry => ModSourcePresentation.GetIconGeometry(Source);
    public string SourceToolTip => ModSourcePresentation.GetToolTip(Source);
    public string IssueLabel => AnalysisHealthLabel;
    public string DependentCountText => TransitiveDependentCount.ToString();
    public string CycleLabel => IsInCycle ? "CYCLE" : string.Empty;

    public void NotifyEvidenceChanged()
    {
        Notify(nameof(ShowEvidenceBadges));
        Notify(nameof(Mod));
    }

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FormatStatusLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        if (value.Equals("HEALTHY", StringComparison.OrdinalIgnoreCase))
        {
            return "Healthy";
        }

        var countMatch = Regex.Match(value, @"(?<count>\d+)\s+(?:ERROR|ISSUE)", RegexOptions.IgnoreCase);
        if (countMatch.Success && int.TryParse(countMatch.Groups["count"].Value, out var count))
        {
            return count == 1 ? "1 Error" : $"{count} Errors";
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
    }
}

