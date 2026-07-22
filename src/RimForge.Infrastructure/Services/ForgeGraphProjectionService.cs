using System.Security.Cryptography;
using System.Text;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

/// <summary>
/// Projects immutable Shared Evidence generations into the ForgeView graph model.
/// Metadata remains authoritative for relationships; Shared Evidence enriches node
/// health and provides stable fingerprints for incremental node reuse.
/// </summary>
public sealed class ForgeGraphProjectionService : IForgeGraphProjectionService
{
    private readonly IDependencyGraphService _dependencyGraphService;
    private readonly object _gate = new();
    private readonly Dictionary<string, CachedNode> _nodeCache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _previousNodeFingerprints = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _previousEdgeKeys = new(StringComparer.OrdinalIgnoreCase);

    public ForgeGraphProjectionService(IDependencyGraphService dependencyGraphService)
    {
        _dependencyGraphService = dependencyGraphService;
    }

    public ForgeGraphProjection Project(
        IReadOnlyList<ModRecord> mods,
        ForgeGraphEvidenceInput evidence)
    {
        ArgumentNullException.ThrowIfNull(mods);
        ArgumentNullException.ThrowIfNull(evidence);

        var started = DateTimeOffset.UtcNow;
        var (baseGraph, missing, cycles) = _dependencyGraphService.Build(mods);
        var currentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nodes = new DependencyGraphNode[baseGraph.Nodes.Count];
        var reused = 0;
        var rebuilt = 0;

        lock (_gate)
        {
            for (var index = 0; index < baseGraph.Nodes.Count; index++)
            {
                var source = baseGraph.Nodes[index];
                var identity = source.PackageId ?? source.Id;
                currentIds.Add(identity);
                var fingerprint = BuildNodeFingerprint(source, evidence);

                if (_nodeCache.TryGetValue(identity, out var cached)
                    && string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    nodes[index] = cached.Node;
                    reused++;
                    continue;
                }

                var enriched = source with { Status = ResolveHealth(source, evidence) };
                _nodeCache[identity] = new CachedNode(fingerprint, enriched);
                nodes[index] = enriched;
                rebuilt++;
            }

            foreach (var removed in _nodeCache.Keys.Where(id => !currentIds.Contains(id)).ToArray())
                _nodeCache.Remove(removed);
        }

        var orderedNodes = nodes
            .OrderBy(node => node.PackageId ?? node.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var orderedEdges = baseGraph.Edges
            .Concat(ProjectEvidenceRelationships(evidence))
            .GroupBy(BuildEdgeKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(edge => edge.DeclarationCount).First())
            .OrderBy(edge => edge.SourceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.TargetId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.Relationship)
            .Select(edge => edge.Provenance is null
                ? edge with { Provenance = ForgeGraphRelationshipProvenance.FromDeclaration(edge) }
                : edge)
            .ToArray();
        var graph = new DependencyGraphModel(orderedNodes, orderedEdges);
        var intelligence = BuildIntelligence(graph, evidence);
        var signature = BuildTopologySignature(graph, evidence.Generation);
        return new ForgeGraphProjection(
            graph,
            missing.OrderBy(item => item.SourcePackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.RequiredPackageId, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            cycles.OrderBy(cycle => string.Join("|", cycle.PackageIds), StringComparer.OrdinalIgnoreCase).ToArray(),
            intelligence,
            new ForgeGraphProjectionMetrics(
                evidence.Generation,
                graph.Nodes.Count,
                graph.Edges.Count,
                reused,
                rebuilt,
                DateTimeOffset.UtcNow - started,
            signature));
    }

    private static IEnumerable<DependencyGraphEdge> ProjectEvidenceRelationships(ForgeGraphEvidenceInput snapshot)
    {
        foreach (var evidence in snapshot.Contributions.Where(item =>
                     !string.IsNullOrWhiteSpace(item.RelatedSubjectId) &&
                     item.Provenance.SourceKind is ForgeEvidenceSourceKind.RuntimeCompanion
                         or ForgeEvidenceSourceKind.CompatibilityIntelligence))
        {
            var conflictScore = evidence.EffectiveAttributes.TryGetValue("conflictScore", out var value) &&
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
            var isConflict = conflictScore >= 0.5 ||
                             evidence.EvidenceType.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
                             evidence.EvidenceType.Contains("incompat", StringComparison.OrdinalIgnoreCase) ||
                             evidence.EvidenceType.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
                             evidence.EvidenceType.Contains("failure", StringComparison.OrdinalIgnoreCase);
            yield return new DependencyGraphEdge(
                evidence.SubjectId,
                evidence.RelatedSubjectId!,
                isConflict ? DependencyRelationshipType.Incompatible : DependencyRelationshipType.PatchTarget,
                $"Forge Evidence: {evidence.Summary}",
                Math.Max(1, evidence.ObservationCount),
                [evidence.Provenance.SourceId],
                new ForgeGraphRelationshipProvenance(
                    evidence.Provenance.SourceKind.ToString(),
                    evidence.Provenance.SourceId,
                    [evidence.EvidenceId],
                    evidence.Summary));
        }
    }

    private ForgeGraphIntelligence BuildIntelligence(
        DependencyGraphModel graph,
        ForgeGraphEvidenceInput snapshot)
    {
        var started = DateTimeOffset.UtcNow;
        var requiredEdges = graph.Edges
            .Where(edge => edge.Relationship == DependencyRelationshipType.Required)
            .ToArray();
        var orderingEdges = graph.Edges
            .Where(edge => edge.Relationship is DependencyRelationshipType.LoadBefore or DependencyRelationshipType.LoadAfter)
            .ToArray();
        var conflictEdges = graph.Edges
            .Where(edge => edge.Relationship == DependencyRelationshipType.Incompatible)
            .ToArray();

        var dependents = requiredEdges
            .GroupBy(edge => edge.TargetId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(edge => edge.SourceId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var clusters = FindStronglyConnectedComponents(graph.Nodes, requiredEdges);
        // Duplicate package IDs are a real library condition and are reported by the
        // analysis engine. ForgeView must remain available so the user can inspect and
        // resolve that finding instead of failing during graph-intelligence publication.
        var currentFingerprints = graph.Nodes
            .GroupBy(node => node.PackageId ?? node.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => BuildNodeFingerprint(
                    group.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
                        .First(),
                    snapshot),
                StringComparer.OrdinalIgnoreCase);
        var currentEdges = graph.Edges.Select(BuildEdgeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ForgeGraphDiff diff;

        lock (_gate)
        {
            diff = new ForgeGraphDiff(
                currentFingerprints.Keys.Except(_previousNodeFingerprints.Keys, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
                _previousNodeFingerprints.Keys.Except(currentFingerprints.Keys, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
                currentFingerprints.Keys.Intersect(_previousNodeFingerprints.Keys, StringComparer.OrdinalIgnoreCase)
                    .Where(id => !string.Equals(currentFingerprints[id], _previousNodeFingerprints[id], StringComparison.Ordinal))
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
                currentEdges.Except(_previousEdgeKeys, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray(),
                _previousEdgeKeys.Except(currentEdges, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray());
            _previousNodeFingerprints = currentFingerprints;
            _previousEdgeKeys = currentEdges;
        }

        return new ForgeGraphIntelligence(
            dependents,
            clusters,
            diff,
            new ForgeGraphIntelligenceMetrics(
                requiredEdges.Length,
                orderingEdges.Length,
                conflictEdges.Length,
                dependents.Sum(pair => pair.Value.Count),
                clusters.Count,
                clusters.Count(cluster => cluster.IsCycle),
                DateTimeOffset.UtcNow - started));
    }

    private static IReadOnlyList<ForgeGraphCluster> FindStronglyConnectedComponents(
        IReadOnlyList<DependencyGraphNode> nodes,
        IReadOnlyList<DependencyGraphEdge> requiredEdges)
    {
        var nodeIds = nodes.Select(node => node.PackageId ?? node.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var adjacency = nodeIds.ToDictionary(
            id => id,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var edge in requiredEdges)
        {
            if (adjacency.TryGetValue(edge.SourceId, out var targets) && adjacency.ContainsKey(edge.TargetId))
                targets.Add(edge.TargetId);
        }
        foreach (var targets in adjacency.Values)
            targets.Sort(StringComparer.OrdinalIgnoreCase);

        var index = 0;
        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lowLinks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clusters = new List<ForgeGraphCluster>();

        foreach (var node in nodeIds)
            if (!indices.ContainsKey(node)) Visit(node);

        return clusters
            .OrderBy(cluster => cluster.Members[0], StringComparer.OrdinalIgnoreCase)
            .ToArray();

        void Visit(string node)
        {
            indices[node] = index;
            lowLinks[node] = index;
            index++;
            stack.Push(node);
            onStack.Add(node);

            foreach (var target in adjacency[node])
            {
                if (!indices.ContainsKey(target))
                {
                    Visit(target);
                    lowLinks[node] = Math.Min(lowLinks[node], lowLinks[target]);
                }
                else if (onStack.Contains(target))
                {
                    lowLinks[node] = Math.Min(lowLinks[node], indices[target]);
                }
            }

            if (lowLinks[node] != indices[node]) return;
            var members = new List<string>();
            string member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                members.Add(member);
            } while (!string.Equals(member, node, StringComparison.OrdinalIgnoreCase));
            members.Sort(StringComparer.OrdinalIgnoreCase);
            var selfLoop = members.Count == 1 && adjacency[members[0]].Contains(members[0], StringComparer.OrdinalIgnoreCase);
            clusters.Add(new ForgeGraphCluster(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", members))))[..12],
                members,
                members.Count > 1 || selfLoop));
        }
    }

    private static string BuildEdgeKey(DependencyGraphEdge edge) =>
        $"{edge.SourceId}>{edge.TargetId}:{edge.Relationship}";

    private static ModHealthStatus ResolveHealth(
        DependencyGraphNode node,
        ForgeGraphEvidenceInput snapshot)
    {
        if (node.Status is ModHealthStatus.Error or ModHealthStatus.Warning)
            return node.Status;

        var entry = snapshot.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.ModId, node.Id, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(node.PackageId)
                && string.Equals(candidate.PackageId, node.PackageId, StringComparison.OrdinalIgnoreCase)));

        if (entry is null)
            return snapshot.Generation == 0 ? node.Status : ModHealthStatus.Unknown;

        if (entry.Evidence.NotableFindings.Count > 0)
            return ModHealthStatus.Warning;

        return ModHealthStatus.Healthy;
    }

    private static string BuildNodeFingerprint(
        DependencyGraphNode node,
        ForgeGraphEvidenceInput snapshot)
    {
        var entry = snapshot.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.ModId, node.Id, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(node.PackageId)
                && string.Equals(candidate.PackageId, node.PackageId, StringComparison.OrdinalIgnoreCase)));
        return string.Join("\u001f",
            node.Id,
            node.Name,
            node.PackageId,
            node.Author,
            node.WorkshopId,
            node.Status,
            entry?.Fingerprint ?? string.Empty,
            entry?.Evidence.NotableFindings.Count ?? 0);
    }

    private static string BuildTopologySignature(DependencyGraphModel graph, int generation)
    {
        var payload = new StringBuilder()
            .Append(generation)
            .Append('|')
            .AppendJoin(';', graph.Nodes
                .Select(node => $"{node.PackageId ?? node.Id}:{node.Status}")
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            .Append('|')
            .AppendJoin(';', graph.Edges
                .Select(edge => $"{edge.SourceId}>{edge.TargetId}:{edge.Relationship}")
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            .ToString();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private sealed record CachedNode(string Fingerprint, DependencyGraphNode Node);
}
