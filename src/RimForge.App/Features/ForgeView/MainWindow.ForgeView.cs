using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RimForge.App.Features.ForgeView;
using RimForge.Core.Models;
using RimForge.Infrastructure.Services;

namespace RimForge.App;

public partial class MainWindow
{
    private ForgeGraphIntelligence? _forgeGraphIntelligence;
    private string? _forgeHoveredPackageId;
    private string? _forgeInteractionPackageId;
    private ForgeGraphRenderCompletedEventArgs? _forgeGraphRenderMetrics;
    private DateTimeOffset _lastForgeRenderTelemetryAt;

    private void ForgeView_RefreshRequested(object sender, RoutedEventArgs e) => RefreshLibrary_Click(sender, e);

    public int ForgeFocusedRelationshipCount
    {
        get
        {
            var packageId = SelectedMod?.PackageId;
            if (string.IsNullOrWhiteSpace(packageId)) return 0;
            return DependencyEdges.Count(edge =>
                ForgeGraphPresentationPolicy.ShouldDisplayEdge(edge) &&
                (string.Equals(edge.SourceId, packageId, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(edge.TargetId, packageId, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public string ForgeFocusDisplayName => SelectedMod?.DisplayName ?? "No mod selected";
    public string ForgeFocusPackageId => SelectedMod?.PackageId ?? "Select a mod in the Mod Sorter or Mod Inspector.";
    public string ForgeFocusSummary => SelectedMod is null
        ? "ForgeView is showing the current graph without a selection focus."
        : ForgeFocusedRelationshipCount switch
        {
            0 => "No dependency relationships are currently recorded for this mod.",
            1 => "1 incoming or outgoing relationship is focused.",
            var count => $"{count} incoming and outgoing relationships are focused."
        };
    public string ForgeFocusedProvenanceSummary
    {
        get
        {
            var packageId = SelectedMod?.PackageId;
            if (string.IsNullOrWhiteSpace(packageId)) return "Select a mod to inspect relationship provenance.";
            var provenance = DependencyEdges
                .Where(ForgeGraphPresentationPolicy.ShouldDisplayEdge)
                .Where(edge => string.Equals(edge.SourceId, packageId, StringComparison.OrdinalIgnoreCase) || string.Equals(edge.TargetId, packageId, StringComparison.OrdinalIgnoreCase))
                .Select(edge => edge.Provenance ?? ForgeGraphRelationshipProvenance.FromDeclaration(edge))
                .DistinctBy(item => $"{item.SourceKind}|{item.SourceId}|{item.Summary}", StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item.SourceKind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.SourceId, StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .Select(item => $"{item.SourceKind}: {item.SourceId} — {item.Summary}")
                .ToArray();
            return provenance.Length == 0 ? "No rendered relationships are focused." : string.Join(Environment.NewLine, provenance);
        }
    }

    public int ForgeFocusedDependencyCount => GetFocusedEdges(DependencyRelationshipType.Required, outgoing: true);
    public int ForgeFocusedDependentCount => GetFocusedEdges(DependencyRelationshipType.Required, outgoing: false);
    public int ForgeFocusedConflictCount => GetFocusedEdges(DependencyRelationshipType.Incompatible, outgoing: null);
    public bool ForgeFocusedIsInCycle => _forgeGraphIntelligence?.Clusters.Any(cluster => cluster.IsCycle && cluster.Members.Contains(SelectedMod?.PackageId ?? string.Empty, StringComparer.OrdinalIgnoreCase)) == true;
    public string ForgeInteractionStatus => string.IsNullOrWhiteSpace(_forgeInteractionPackageId)
        ? "Click a graph node to synchronize ForgeView with the Mod Inspector."
        : $"Inspector synchronized to {_forgeInteractionPackageId}.";
    public string ForgeHoverSummary => string.IsNullOrWhiteSpace(_forgeHoveredPackageId)
        ? "Hover a node for dependency intelligence."
        : BuildHoverSummary(_forgeHoveredPackageId);

    public string ForgeGraphRenderStatus => _forgeGraphRenderMetrics is null
        ? "Render telemetry waiting for the graph viewport."
        : $"Rendered {_forgeGraphRenderMetrics.RenderedNodes}/{_forgeGraphRenderMetrics.TotalNodes} nodes and {_forgeGraphRenderMetrics.RenderedEdges}/{_forgeGraphRenderMetrics.TotalEdges} edges in {_forgeGraphRenderMetrics.Elapsed.TotalMilliseconds:0.0} ms" +
          (_forgeGraphRenderMetrics.ViewportCullingEnabled ? $" • culled {_forgeGraphRenderMetrics.CulledNodes} nodes" : " • full topology") +
          (_forgeGraphRenderMetrics.LayoutPending ? " • layout pending" : $" • layout {_forgeGraphRenderMetrics.LayoutElapsed.TotalMilliseconds:0.0} ms") +
          (_forgeGraphRenderMetrics.LayoutCacheHit ? " • cache hit" : string.Empty) +
          $" • cache {_forgeGraphRenderMetrics.LayoutCacheEntries}/8" +
          (_forgeGraphRenderMetrics.Elapsed > ForgeGraphPerformanceBudgets.RenderBudget ? " • over render budget" : string.Empty);

    public void SetForgeGraphRenderMetrics(ForgeGraphRenderCompletedEventArgs metrics)
    {
        var topologyChanged = _forgeGraphRenderMetrics is null
            || _forgeGraphRenderMetrics.TotalNodes != metrics.TotalNodes
            || _forgeGraphRenderMetrics.RenderedNodes != metrics.RenderedNodes
            || _forgeGraphRenderMetrics.TotalEdges != metrics.TotalEdges
            || _forgeGraphRenderMetrics.RenderedEdges != metrics.RenderedEdges
            || _forgeGraphRenderMetrics.ViewportCullingEnabled != metrics.ViewportCullingEnabled
            || _forgeGraphRenderMetrics.LayoutPending != metrics.LayoutPending
            || _forgeGraphRenderMetrics.LayoutCacheHit != metrics.LayoutCacheHit
            || _forgeGraphRenderMetrics.LayoutGeneration != metrics.LayoutGeneration;
        var now = DateTimeOffset.UtcNow;
        if (!topologyChanged && now - _lastForgeRenderTelemetryAt < TimeSpan.FromMilliseconds(500)) return;
        _forgeGraphRenderMetrics = metrics;
        _lastForgeRenderTelemetryAt = now;
        Notify(nameof(ForgeGraphRenderStatus));
    }

    public void SetForgeHoveredPackage(string? packageId)
    {
        if (string.Equals(_forgeHoveredPackageId, packageId, StringComparison.OrdinalIgnoreCase)) return;
        _forgeHoveredPackageId = packageId;
        Notify(nameof(ForgeHoverSummary));
    }

    public void SetForgeInteractionSelection(string packageId)
    {
        _forgeInteractionPackageId = packageId;
        Notify(nameof(ForgeInteractionStatus));
    }

    private string BuildHoverSummary(string packageId)
    {
        var visibleEdges = DependencyEdges.Where(ForgeGraphPresentationPolicy.ShouldDisplayEdge).ToArray();
        var dependencies = visibleEdges.Count(edge => edge.Relationship == DependencyRelationshipType.Required && string.Equals(edge.SourceId, packageId, StringComparison.OrdinalIgnoreCase));
        var dependents = visibleEdges.Count(edge => edge.Relationship == DependencyRelationshipType.Required && string.Equals(edge.TargetId, packageId, StringComparison.OrdinalIgnoreCase));
        var conflicts = visibleEdges.Count(edge => edge.Relationship == DependencyRelationshipType.Incompatible && (string.Equals(edge.SourceId, packageId, StringComparison.OrdinalIgnoreCase) || string.Equals(edge.TargetId, packageId, StringComparison.OrdinalIgnoreCase)));
        var cycle = _forgeGraphIntelligence?.Clusters.Any(cluster => cluster.IsCycle && cluster.Members.Contains(packageId, StringComparer.OrdinalIgnoreCase)) == true;
        return $"{packageId}: {dependencies} dependencies • {dependents} dependents • {conflicts} conflicts" + (cycle ? " • cycle member" : string.Empty);
    }

    private int GetFocusedEdges(DependencyRelationshipType relationship, bool? outgoing)
    {
        var packageId = SelectedMod?.PackageId;
        if (string.IsNullOrWhiteSpace(packageId)) return 0;
        return DependencyEdges.Count(edge => ForgeGraphPresentationPolicy.ShouldDisplayEdge(edge) && edge.Relationship == relationship && outgoing switch
        {
            true => string.Equals(edge.SourceId, packageId, StringComparison.OrdinalIgnoreCase),
            false => string.Equals(edge.TargetId, packageId, StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(edge.SourceId, packageId, StringComparison.OrdinalIgnoreCase) || string.Equals(edge.TargetId, packageId, StringComparison.OrdinalIgnoreCase)
        });
    }

    private void NotifyForgeSelectionContext()
    {
        Notify(nameof(ForgeFocusedRelationshipCount));
        Notify(nameof(ForgeFocusDisplayName));
        Notify(nameof(ForgeFocusPackageId));
        Notify(nameof(ForgeFocusSummary));
        Notify(nameof(ForgeFocusedProvenanceSummary));
        Notify(nameof(ForgeFocusedDependencyCount));
        Notify(nameof(ForgeFocusedDependentCount));
        Notify(nameof(ForgeFocusedConflictCount));
        Notify(nameof(ForgeFocusedIsInCycle));
        Notify(nameof(ForgeInteractionStatus));
        Notify(nameof(ForgeHoverSummary));
        DependencyEdgesView.Refresh();
    }
}
