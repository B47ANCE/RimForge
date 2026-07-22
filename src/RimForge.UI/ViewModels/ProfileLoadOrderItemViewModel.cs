using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using RimForge.Core.Models;
using RimForge.UI.Presentation;

namespace RimForge.UI.ViewModels;

public sealed class ProfileLoadOrderItemViewModel : INotifyPropertyChanged
{
    private int _position;
    private bool _isEnabled;
    private bool _isDragGhost;

    public ProfileLoadOrderItemViewModel(int position, string packageId, ModRecord? mod, bool isEnabled = true, int analysisIssueCount = 0, bool isInCycle = false, string? analysisHealthLabel = null, int ignoredIssueCount = 0, bool analysisIsStale = false)
    {
        _position = position;
        PackageId = packageId;
        Mod = mod;
        _isEnabled = isEnabled;
        AnalysisIssueCount = analysisIssueCount;
        IsInCycle = isInCycle;
        AnalysisHealthLabel = analysisHealthLabel;
        IgnoredIssueCount = ignoredIssueCount;
        AnalysisIsStale = analysisIsStale;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public int Position { get => _position; set { if (_position == value) return; _position = value; Notify(); Notify(nameof(PositionText)); } }
    public string PositionText => IsEnabled ? $"#{Position}" : "OFF";
    public string PackageId { get; }
    public int AnalysisIssueCount { get; }
    public bool IsInCycle { get; }
    public string? AnalysisHealthLabel { get; }
    public int IgnoredIssueCount { get; }
    public bool AnalysisIsStale { get; }
    public ModRecord? Mod { get; }
    public string DisplayName => Mod?.DisplayName ?? PackageId;
    public bool IsOfficialContent => ModRecord.IsOfficialRimWorldPackageId(PackageId);
    public ModSource Source => IsOfficialContent
        ? ModSource.Official
        : Mod?.Source ?? ModSource.Local;
    public string SourceLabel => ModSourcePresentation.GetDisplayName(Source);
    public bool IsCore => PackageId.Equals(LoadOrderRules.CorePackageId, StringComparison.OrdinalIgnoreCase);
    public bool IsLoadOrderAnchor => LoadOrderRules.IsPositionAnchor(PackageId);
    public bool IsMandatory => LoadOrderRules.IsMandatory(PackageId);
    public bool CanDeactivate => LoadOrderRules.CanDeactivate(PackageId);
    public bool IsTopLoadOrderAnchor => LoadOrderRules.IsTopAnchor(PackageId);
    public bool IsBottomLoadOrderAnchor => LoadOrderRules.IsBottomAnchor(PackageId);
    public bool ShowHealthIndicator => !IsOfficialContent;
    public bool ShowEvidenceBadges => !IsOfficialContent && VisibleEvidenceBadges.Count > 0;
    public IReadOnlyList<ModEvidenceBadge> VisibleEvidenceBadges => Mod?.Evidence.VisibleBadges ?? Array.Empty<ModEvidenceBadge>();
    public bool HasHiddenEvidenceBadges => Mod?.Evidence.HasHiddenBadges == true;
    public string HiddenEvidenceBadgeText => Mod?.Evidence.HiddenBadgeText ?? string.Empty;
    public string EvidenceDnaText => Mod?.Evidence.DnaText ?? string.Empty;
    public Geometry SourceIconGeometry => ModSourcePresentation.GetIconGeometry(Source);
    public string SourceToolTip => ModSourcePresentation.GetToolTip(Source);
    public bool IsOfficialDlc => PackageId.StartsWith("ludeon.rimworld.", StringComparison.OrdinalIgnoreCase);
    public bool IsEnabled { get => _isEnabled; set { if (_isEnabled == value || IsCore) return; _isEnabled = value; Notify(); Notify(nameof(PositionText)); } }
    public bool IsDragGhost { get => _isDragGhost; set { if (_isDragGhost == value) return; _isDragGhost = value; Notify(); } }
    public bool CanToggle => IsOfficialDlc && !IsCore;
    public string HealthState
    {
        get
        {
            if (Mod is null) return "Pending";
            if (IsInCycle || Mod.Errors.Count > 0 || AnalysisHealthLabel?.Contains("ERROR", StringComparison.OrdinalIgnoreCase) == true)
                return "Critical";
            if (AnalysisIssueCount > 0 || AnalysisHealthLabel?.Contains("WARNING", StringComparison.OrdinalIgnoreCase) == true)
                return "Warning";
            return "Healthy";
        }
    }

    public void NotifyEvidenceChanged()
    {
        Notify(nameof(VisibleEvidenceBadges));
        Notify(nameof(HasHiddenEvidenceBadges));
        Notify(nameof(HiddenEvidenceBadgeText));
        Notify(nameof(EvidenceDnaText));
        Notify(nameof(ShowEvidenceBadges));
        Notify(nameof(Mod));
    }

    public string HealthToolTip
    {
        get
        {
            if (Mod is null || string.IsNullOrWhiteSpace(AnalysisHealthLabel)) return "Not analyzed — click to open Mod Inspector.";
            if (AnalysisIsStale) return "Stale analysis — cached results may not match the current profile. Open Mod Inspector.";
            if (AnalysisIssueCount > 0) return $"Active issues — {AnalysisIssueCount} finding(s). Click to open Issue Viewer filtered to this mod.";
            if (IgnoredIssueCount > 0) return $"Ignored-only findings — {IgnoredIssueCount} ignored finding(s). Click to open Issue Viewer with ignored findings visible.";
            return "Healthy — click to open Mod Inspector.";
        }
    }

    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
