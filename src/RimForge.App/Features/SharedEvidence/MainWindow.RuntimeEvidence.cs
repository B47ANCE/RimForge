using System.Collections.ObjectModel;
using RimForge.Core.Models;
using RimForge.Infrastructure.Services;

namespace RimForge.App;

public partial class MainWindow
{
    public ObservableCollection<RuntimeEvidenceRecord> RuntimeEvidenceItems { get; } = new();
    public ObservableCollection<CompatibilityIntelligence> RuntimeCompatibilityItems { get; } = new();

    public int RuntimeEvidenceCount => RuntimeEvidenceItems.Sum(item => Math.Max(1, item.OccurrenceCount));
    public int RuntimeConflictCount => RuntimeEvidenceItems.Count(IsRuntimeConflict);
    public int RuntimeSessionCount => _applicationServices.RuntimeEvidenceStore.Current.Sessions.Count;
    public string RuntimeSensorStatusText => _applicationServices.RuntimeSensorHost.IsListening
        ? "Runtime Sensor listening"
        : "Runtime Sensor offline";
    public string RuntimeEvidenceSummaryText => RuntimeEvidenceCount == 0
        ? "No runtime evidence has been collected yet."
        : $"{RuntimeEvidenceCount:N0} observations across {RuntimeSessionCount:N0} sessions • {RuntimeConflictCount:N0} conflicts";

    public IReadOnlyList<RuntimeEvidenceRecord> SelectedRuntimeEvidence
    {
        get
        {
            var packageId = SelectedMod?.PackageId;
            if (string.IsNullOrWhiteSpace(packageId)) return Array.Empty<RuntimeEvidenceRecord>();
            return RuntimeEvidenceItems
                .Where(item => string.Equals(item.SourcePackageId, packageId, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(item.TargetPackageId, packageId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.LastObservedUtc)
                .ToArray();
        }
    }

    public string SelectedRuntimeEvidenceSummary => SelectedMod is null
        ? "Select a mod to inspect runtime evidence."
        : SelectedRuntimeEvidence.Count == 0
            ? "No runtime observations are associated with this mod."
            : $"{SelectedRuntimeEvidence.Sum(item => Math.Max(1, item.OccurrenceCount)):N0} observations • {SelectedRuntimeEvidence.Count(IsRuntimeConflict):N0} conflicts";

    private async Task InitializeRuntimeEvidenceAsync(CancellationToken cancellationToken)
    {
        _applicationServices.RuntimeEvidenceStore.SnapshotChanged += RuntimeEvidenceStore_SnapshotChanged;
        _applicationServices.RuntimeSensorHost.ListeningChanged += RuntimeSensorHost_ListeningChanged;
        var snapshot = await _applicationServices.RuntimeEvidenceStore.LoadAsync(cancellationToken);
        ProjectRuntimeEvidence(snapshot);
        await _applicationServices.RuntimeSensorHost.StartAsync(cancellationToken);
        Notify(nameof(RuntimeSensorStatusText));
    }

    private void RuntimeEvidenceStore_SnapshotChanged(object? sender, RuntimeEvidenceSnapshot snapshot) =>
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ProjectRuntimeEvidence(snapshot);
            _forgeEvidenceService.Invalidate(RepositoryRoot, ForgeEvidenceInvalidationReason.RuntimeEvidenceChanged);
        }));

    private void RuntimeSensorHost_ListeningChanged(object? sender, bool listening) =>
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Notify(nameof(RuntimeSensorStatusText));
            Notify(nameof(CompanionHostStatusText));
        }));

    private void ProjectRuntimeEvidence(RuntimeEvidenceSnapshot snapshot)
    {
        RuntimeEvidenceItems.Clear();
        foreach (var item in snapshot.Evidence.OrderByDescending(item => item.LastObservedUtc))
            RuntimeEvidenceItems.Add(item);
        RuntimeCompatibilityItems.Clear();
        foreach (var item in snapshot.Compatibility.OrderByDescending(item => item.ConflictScore))
            RuntimeCompatibilityItems.Add(item);
        NotifyRuntimeEvidenceProperties();
    }

    private void NotifyRuntimeEvidenceProperties()
    {
        Notify(nameof(RuntimeEvidenceCount));
        Notify(nameof(RuntimeConflictCount));
        Notify(nameof(RuntimeSessionCount));
        Notify(nameof(RuntimeEvidenceSummaryText));
        Notify(nameof(SelectedRuntimeEvidence));
        Notify(nameof(SelectedRuntimeEvidenceSummary));
    }

    private static bool IsRuntimeConflict(RuntimeEvidenceRecord item) =>
        item.RelationshipKind.Contains("incompat", StringComparison.OrdinalIgnoreCase) ||
        item.Kind.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
        item.Kind.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
        item.Kind.Contains("failure", StringComparison.OrdinalIgnoreCase) ||
        item.Kind.Contains("missing-dependency", StringComparison.OrdinalIgnoreCase);
}
