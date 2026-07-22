using System.IO;
using RimForge.Core.Models;
using RimForge.Infrastructure.Services;

namespace RimForge.App;

public partial class MainWindow
{
    public int SharedEvidenceGeneration { get; private set; }
    public int ForgeGraphEvidenceGeneration { get; private set; }
    public string ForgeGraphProjectionStatus { get; private set; } = "ForgeView is using the library graph baseline.";
    public string ForgeGraphIntelligenceStatus { get; private set; } = "Graph intelligence is waiting for Shared Evidence.";
    public int ForgeGraphCycleClusterCount { get; private set; }
    public string SharedEvidenceStatusText => _forgeEvidenceSnapshot.Generation == 0
        ? "Shared evidence has not been published yet."
        : $"Generation {_forgeEvidenceSnapshot.Generation}: {_forgeEvidenceSnapshot.Metrics.Scanned} scanned, {_forgeEvidenceSnapshot.Metrics.Reused} cache hits, {_forgeEvidenceSnapshot.Metrics.PendingInvalidations} invalidations, {_forgeEvidenceSnapshot.Metrics.ActiveWatchers} watchers, {_forgeEvidenceSnapshot.Metrics.CoalescedRequests} requests coalesced, {_forgeEvidenceSnapshot.Metrics.DebouncedInvalidations} watcher bursts debounced, {_forgeEvidenceSnapshot.Metrics.WatcherOverflows} watcher overflows";

    private void ForgeEvidenceBus_Published(object? sender, ForgeEvidencePublication publication)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => ForgeEvidenceBus_Published(sender, publication));
            return;
        }

        ApplyForgeEvidenceSnapshot(publication.Snapshot);
    }

    private void ForgeEvidenceService_Invalidated(object? sender, string rootPath)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => ForgeEvidenceService_Invalidated(sender, rootPath));
            return;
        }

        var mod = Mods.FirstOrDefault(item =>
            string.Equals(Path.GetFullPath(item.RootPath), rootPath, StringComparison.OrdinalIgnoreCase));
        if (mod is not null)
            Append($"Shared evidence invalidated for {mod.DisplayName}. The next refresh will rescan only this mod.", ActivitySeverity.Info);
    }

    private void ApplyForgeEvidenceSnapshot(ForgeEvidenceSnapshot snapshot)
    {
        _forgeEvidenceSnapshot = snapshot;
        SharedEvidenceGeneration = snapshot.Generation;
        var evidenceChangedMods = new List<ModRecord>();
        foreach (var mod in Mods)
        {
            if (!snapshot.TryGet(mod.Id, out var entry) || entry is null)
                continue;

            if (!ReferenceEquals(mod.Evidence, entry.Evidence))
            {
                mod.Evidence = entry.Evidence;
                evidenceChangedMods.Add(mod);
            }
        }

        foreach (var mod in evidenceChangedMods)
            ApplyBackgroundIntelligenceUpdate(mod);

        var graphEvidence = new ForgeGraphEvidenceInput(
            snapshot.Generation,
            snapshot.Entries.Values.Select(entry => new ForgeGraphEvidenceEntry(
                entry.ModId,
                entry.PackageId,
                entry.Fingerprint,
                entry.Evidence)).ToArray(),
            snapshot.Contributions);
        var projection = _forgeGraphProjectionService.Project(Mods.ToArray(), graphEvidence);
        var orderedNodes = projection.Graph.Nodes
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.PackageId ?? node.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var orderedEdges = projection.Graph.Edges
            .GroupBy(edge => $"{edge.SourceId}>{edge.TargetId}:{edge.Relationship}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(edge => edge.DeclarationCount).First())
            .OrderBy(edge => edge.SourceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.TargetId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.Relationship)
            .ToArray();
        var graphDiffSize = projection.Intelligence.Diff.AddedNodes.Count
            + projection.Intelligence.Diff.RemovedNodes.Count
            + projection.Intelligence.Diff.ChangedNodes.Count
            + projection.Intelligence.Diff.AddedEdges.Count
            + projection.Intelligence.Diff.RemovedEdges.Count;
        var appliedIncrementally = graphDiffSize > 0 && graphDiffSize <= 64
            && TrySynchronizeGraphCollections(orderedNodes, orderedEdges);
        if (!appliedIncrementally)
        {
            DependencyNodes.ReplaceAll(orderedNodes);
            DependencyEdges.ReplaceAll(orderedEdges);
        }
        GraphNodeCount = projection.Graph.Nodes.Count;
        GraphEdgeCount = projection.Graph.Edges.Count;
        ForgeGraphEvidenceGeneration = projection.Metrics.EvidenceGeneration;
        ForgeGraphProjectionStatus = $"Generation {projection.Metrics.EvidenceGeneration}: {projection.Metrics.RebuiltNodes} graph nodes rebuilt, {projection.Metrics.ReusedNodes} reused in {projection.Metrics.Elapsed.TotalMilliseconds:0.0} ms • {(appliedIncrementally ? "incremental collection update" : "atomic full projection")}.";
        _forgeGraphIntelligence = projection.Intelligence;
        ForgeGraphCycleClusterCount = projection.Intelligence.Metrics.CyclicComponents;
        ForgeGraphIntelligenceStatus = $"{projection.Intelligence.Metrics.RequiredEdges} dependencies, {projection.Intelligence.Metrics.OrderingEdges} ordering rules, {projection.Intelligence.Metrics.ConflictEdges} conflicts, {ForgeGraphCycleClusterCount} cycle clusters; diff +{projection.Intelligence.Diff.AddedNodes.Count}/-{projection.Intelligence.Diff.RemovedNodes.Count}/~{projection.Intelligence.Diff.ChangedNodes.Count}.";

        Notify(nameof(SharedEvidenceGeneration));
        Notify(nameof(ForgeGraphEvidenceGeneration));
        Notify(nameof(ForgeGraphProjectionStatus));
        Notify(nameof(ForgeGraphIntelligenceStatus));
        Notify(nameof(ForgeGraphCycleClusterCount));
        NotifyForgeSelectionContext();
        Notify(nameof(GraphNodeCount));
        Notify(nameof(GraphEdgeCount));
        Notify(nameof(SharedEvidenceStatusText));
        Notify(nameof(SharedEvidenceDiagnostics));
        Notify(nameof(SharedEvidenceDiagnosticsText));
        NotifyForgeEvidenceSelectionProperties();
        NotifyAnalysisProperties();
        Notify(nameof(SelectedMod));
    }

    private bool TrySynchronizeGraphCollections(
        IReadOnlyList<DependencyGraphNode> nodes,
        IReadOnlyList<DependencyGraphEdge> edges)
    {
        try
        {
            SynchronizeCollection(
                DependencyNodes,
                nodes,
                node => node.PackageId ?? node.Id,
                StringComparer.OrdinalIgnoreCase);
            SynchronizeCollection(
                DependencyEdges,
                edges,
                edge => $"{edge.SourceId}>{edge.TargetId}:{edge.Relationship}",
                StringComparer.OrdinalIgnoreCase);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void SynchronizeCollection<T>(
        System.Collections.ObjectModel.ObservableCollection<T> target,
        IReadOnlyList<T> desired,
        Func<T, string> keySelector,
        IEqualityComparer<string> comparer)
    {
        var desiredKeys = desired.Select(keySelector).ToHashSet(comparer);
        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!desiredKeys.Contains(keySelector(target[index])))
                target.RemoveAt(index);
        }

        for (var index = 0; index < desired.Count; index++)
        {
            var desiredItem = desired[index];
            var desiredKey = keySelector(desiredItem);
            var currentIndex = -1;
            for (var candidate = index; candidate < target.Count; candidate++)
            {
                if (comparer.Equals(keySelector(target[candidate]), desiredKey))
                {
                    currentIndex = candidate;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                target.Insert(index, desiredItem);
                continue;
            }

            if (currentIndex != index)
                target.Move(currentIndex, index);
            if (!EqualityComparer<T>.Default.Equals(target[index], desiredItem))
                target[index] = desiredItem;
        }
    }

    private void InvalidateSharedEvidence(ModRecord mod, ForgeEvidenceInvalidationReason reason) =>
        _forgeEvidenceService.Invalidate(mod.RootPath, reason);
}

public partial class MainWindow
{
    public ForgeEvidenceDiagnostics SharedEvidenceDiagnostics =>
        _forgeEvidenceQueryService.Diagnose(_forgeEvidenceSnapshot);

    public int SelectedForgeEvidenceCount => SelectedMod?.PackageId is not { Length: > 0 } packageId
        ? 0
        : _forgeEvidenceSnapshot.Index.ForSubjectOrRelated(packageId).Count;

    public string SelectedForgeEvidenceSummary => SelectedMod?.PackageId is not { Length: > 0 } packageId
        ? "Select a mod to inspect unified Forge Evidence."
        : SelectedForgeEvidenceCount == 0
            ? "No unified evidence is currently indexed for this mod."
            : $"{SelectedForgeEvidenceCount} unified evidence record(s) across " +
              $"{_forgeEvidenceSnapshot.Index.ForSubjectOrRelated(packageId).Select(item => item.Provenance.SourceKind).Distinct().Count()} source(s).";

    public IReadOnlyList<ForgeEvidenceContribution> SelectedForgeEvidence =>
        SelectedMod?.PackageId is not { Length: > 0 } packageId
            ? Array.Empty<ForgeEvidenceContribution>()
            : _forgeEvidenceQueryService.Query(
                _forgeEvidenceSnapshot,
                new ForgeEvidenceQuery(SubjectIds: new[] { packageId }, Limit: 500))
              .Items;

    public string SharedEvidenceDiagnosticsText
    {
        get
        {
            var diagnostics = SharedEvidenceDiagnostics;
            return diagnostics.Generation == 0
                ? "Forge Evidence diagnostics are waiting for the first published generation."
                : $"{diagnostics.ContributionCount} contributions • {diagnostics.SubjectCount} subjects • " +
                  $"{diagnostics.RelationshipCount} relationships • {diagnostics.DiagnosticCount} producer diagnostics";
        }
    }

    private void NotifyForgeEvidenceSelectionProperties()
    {
        Notify(nameof(SelectedForgeEvidenceCount));
        Notify(nameof(SelectedForgeEvidenceSummary));
        Notify(nameof(SelectedForgeEvidence));
    }
}
