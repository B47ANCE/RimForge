using RimForge.Core.Models;

namespace RimForge.Core.Services;

public interface IForgeGraphQueryService
{
    ForgeGraphQueryResult Execute(DependencyGraphModel graph, ForgeGraphQuery query);
}

public sealed class ForgeGraphQueryService : IForgeGraphQueryService
{
    public ForgeGraphQueryResult Execute(DependencyGraphModel graph, ForgeGraphQuery query)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(query);

        var nodes = graph.Nodes
            .GroupBy(NodeId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase).ThenBy(node => node.Id, StringComparer.OrdinalIgnoreCase).First())
            .ToDictionary(NodeId, StringComparer.OrdinalIgnoreCase);
        var edges = graph.Edges
            .Where(edge => nodes.ContainsKey(edge.SourceId) && nodes.ContainsKey(edge.TargetId))
            .Select(EnsureProvenance)
            .OrderBy(edge => edge.SourceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.TargetId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.Relationship)
            .ToArray();

        var totalNodes = nodes.Count;
        var totalEdges = edges.Length;
        var included = nodes.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!query.ShowFullLibrary && query.EffectiveProfilePackageIds.Count > 0)
            included.IntersectWith(query.EffectiveProfilePackageIds);
        if (query.SearchActive)
            included.IntersectWith(query.EffectiveSearchPackageIds);
        if (query.Health is { } health)
            included.IntersectWith(nodes.Values.Where(node => node.Status == health).Select(NodeId));

        var relationships = query.EffectiveRelationships.ToHashSet();
        var filteredEdges = edges
            .Where(edge => relationships.Count == 0 || relationships.Contains(edge.Relationship))
            .Where(edge => included.Contains(edge.SourceId) && included.Contains(edge.TargetId))
            .ToArray();

        if (query.IsolateFocusedPath && !string.IsNullOrWhiteSpace(query.FocusPackageId) && included.Contains(query.FocusPackageId))
        {
            var focused = Reachable(query.FocusPackageId, filteredEdges);
            included.IntersectWith(focused);
            filteredEdges = filteredEdges.Where(edge => included.Contains(edge.SourceId) && included.Contains(edge.TargetId)).ToArray();
        }

        var orderedNodes = included.Select(id => nodes[id])
            .OrderBy(node => NodeId(node), StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ForgeGraphQueryResult(orderedNodes, filteredEdges, totalNodes, totalEdges);
    }

    private static HashSet<string> Reachable(string focus, IReadOnlyList<DependencyGraphEdge> edges)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { focus };
        var queue = new Queue<string>();
        queue.Enqueue(focus);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in edges.Where(edge =>
                         string.Equals(edge.SourceId, current, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(edge.TargetId, current, StringComparison.OrdinalIgnoreCase))
                     .Select(edge => string.Equals(edge.SourceId, current, StringComparison.OrdinalIgnoreCase) ? edge.TargetId : edge.SourceId)
                     .OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
                if (result.Add(next)) queue.Enqueue(next);
        }
        return result;
    }

    private static DependencyGraphEdge EnsureProvenance(DependencyGraphEdge edge) =>
        edge.Provenance is null ? edge with { Provenance = ForgeGraphRelationshipProvenance.FromDeclaration(edge) } : edge;

    private static string NodeId(DependencyGraphNode node) => node.PackageId ?? node.Id;
}

public sealed class ForgeGraphSelectionState
{
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    public ForgeGraphSelectionSnapshot Current { get; private set; } =
        new(null, null, ForgeGraphQueryOrigin.Canvas, Array.Empty<string>(), -1);

    public ForgeGraphSelectionSnapshot Select(string? packageId, ForgeGraphQueryOrigin origin, bool focus = true)
    {
        var normalized = string.IsNullOrWhiteSpace(packageId) ? null : packageId.Trim();
        if (normalized is not null && (_historyIndex < 0 || !string.Equals(_history[_historyIndex], normalized, StringComparison.OrdinalIgnoreCase)))
        {
            if (_historyIndex < _history.Count - 1)
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            _history.Add(normalized);
            _historyIndex = _history.Count - 1;
        }
        Current = Snapshot(normalized, focus ? normalized : Current.FocusedPackageId, origin);
        return Current;
    }

    public ForgeGraphSelectionSnapshot Navigate(int offset)
    {
        var next = _historyIndex + offset;
        if (next < 0 || next >= _history.Count) return Current;
        _historyIndex = next;
        return Current = Snapshot(_history[next], _history[next], ForgeGraphQueryOrigin.History);
    }

    public void Restore(IEnumerable<string>? history, int historyIndex, string? selectedPackageId, string? focusedPackageId)
    {
        _history.Clear();
        if (history is not null)
            foreach (var id in history.Where(id => !string.IsNullOrWhiteSpace(id)))
                if (_history.Count == 0 || !string.Equals(_history[^1], id, StringComparison.OrdinalIgnoreCase)) _history.Add(id.Trim());
        _historyIndex = _history.Count == 0 ? -1 : Math.Clamp(historyIndex, 0, _history.Count - 1);
        Current = Snapshot(selectedPackageId, focusedPackageId, ForgeGraphQueryOrigin.History);
    }

    private ForgeGraphSelectionSnapshot Snapshot(string? selected, string? focused, ForgeGraphQueryOrigin origin) =>
        new(selected, focused, origin, _history.ToArray(), _historyIndex);
}
