using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class DependencyGraphService : IDependencyGraphService
{
    public (DependencyGraphModel Graph, IReadOnlyList<MissingDependency> Missing, IReadOnlyList<DependencyCycle> Cycles)
        Build(IReadOnlyList<ModRecord> mods)
    {
        var byPackageId = mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
            .GroupBy(mod => mod.PackageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var nodes = mods.Select(ToNode).ToArray();
        var edgeDeclarations = new List<DependencyGraphEdge>();
        var missing = new List<MissingDependency>();
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods.Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId)))
        {
            var sourceId = mod.PackageId!;
            if (!adjacency.ContainsKey(sourceId))
                adjacency[sourceId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var dependencies = mod.Dependencies
                .Concat(LoadOrderRules.GetCanonicalDependencies(mod.PackageId))
                .GroupBy(dependency => dependency.PackageId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(dependency => string.Equals(
                        dependency.Source,
                        "RimForge canonical official-content rule",
                        StringComparison.OrdinalIgnoreCase))
                    .First());

            foreach (var dependency in dependencies)
            {
                if (byPackageId.TryGetValue(dependency.PackageId, out var targetMod))
                {
                    edgeDeclarations.Add(new DependencyGraphEdge(
                        sourceId,
                        dependency.PackageId,
                        DependencyRelationshipType.Required,
                        $"{mod.DisplayName} depends on {targetMod.DisplayName}",
                        DeclarationSources: new[] { dependency.Source ?? "About.xml dependency" }));
                    adjacency[sourceId].Add(dependency.PackageId);
                }
                else
                {
                    missing.Add(new MissingDependency(sourceId, mod.DisplayName, dependency.PackageId, dependency.DisplayName));
                }
            }

            AddOrderingEdges(mod, mod.LoadBefore, DependencyRelationshipType.LoadBefore, edgeDeclarations, byPackageId);
            AddOrderingEdges(mod, mod.LoadAfter, DependencyRelationshipType.LoadAfter, edgeDeclarations, byPackageId);
            AddIncompatibilityEdges(mod, edgeDeclarations, byPackageId);
        }

        var edges = edgeDeclarations
            .GroupBy(edge => new EdgeKey(edge.SourceId, edge.TargetId, edge.Relationship), EdgeKeyComparer.Instance)
            .Select(group =>
            {
                var first = group
                    .OrderBy(edge => edge.Relationship == DependencyRelationshipType.Required ? 0 : 1)
                    .ThenBy(edge => edge.Relationship)
                    .First();
                var sources = group
                    .SelectMany(edge => (edge.DeclarationSources ?? Array.Empty<string>())
                        .Select(source => $"{edge.Relationship}: {source}"))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return first with { DeclarationCount = group.Count(), DeclarationSources = sources };
            })
            .OrderBy(edge => edge.SourceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.TargetId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.Relationship)
            .ToArray();

        var cycles = FindCycles(adjacency);
        return (
            new DependencyGraphModel(nodes, edges),
            missing
                .GroupBy(item => $"{item.SourcePackageId}{item.RequiredPackageId}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray(),
            cycles);
    }

    private static DependencyGraphNode ToNode(ModRecord mod) => new(
        mod.Id, mod.DisplayName, mod.PackageId, mod.Author, mod.WorkshopId, mod.WorkshopUrl,
        mod.RootPath, mod.SupportedVersions, mod.LastModified,
        mod.Errors.Count == 0 ? ModHealthStatus.Healthy : ModHealthStatus.Warning);

    private static void AddOrderingEdges(ModRecord mod, IReadOnlyList<string> targets,
        DependencyRelationshipType type, ICollection<DependencyGraphEdge> edges,
        IReadOnlyDictionary<string, ModRecord> byPackageId)
    {
        if (string.IsNullOrWhiteSpace(mod.PackageId)) return;
        foreach (var target in targets.Where(byPackageId.ContainsKey))
        {
            var verb = type == DependencyRelationshipType.LoadBefore ? "loads before" : "loads after";
            edges.Add(new DependencyGraphEdge(
                mod.PackageId, target, type,
                $"{mod.DisplayName} {verb} {byPackageId[target].DisplayName}",
                DeclarationSources: new[] { type == DependencyRelationshipType.LoadBefore ? "loadBefore" : "loadAfter" }));
        }
    }

    private static void AddIncompatibilityEdges(
        ModRecord mod,
        ICollection<DependencyGraphEdge> edges,
        IReadOnlyDictionary<string, ModRecord> byPackageId)
    {
        if (string.IsNullOrWhiteSpace(mod.PackageId)) return;
        foreach (var target in mod.IncompatibleWith
                     .Where(byPackageId.ContainsKey)
                     .Where(target => !target.Equals(mod.PackageId, StringComparison.OrdinalIgnoreCase)))
        {
            var sourceId = mod.PackageId!;
            var canonicalSource = string.Compare(sourceId, target, StringComparison.OrdinalIgnoreCase) <= 0 ? sourceId : target;
            var canonicalTarget = string.Equals(canonicalSource, sourceId, StringComparison.OrdinalIgnoreCase) ? target : sourceId;
            edges.Add(new DependencyGraphEdge(
                canonicalSource,
                canonicalTarget,
                DependencyRelationshipType.Incompatible,
                $"{mod.DisplayName} is incompatible with {byPackageId[target].DisplayName}",
                DeclarationSources: new[] { $"incompatibleWith on {sourceId}" }));
        }
    }

    private static IReadOnlyList<DependencyCycle> FindCycles(IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();
        var cycleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cycles = new List<DependencyCycle>();
        foreach (var node in adjacency.Keys) Visit(node);
        return cycles;

        void Visit(string node)
        {
            if (state.TryGetValue(node, out var current))
            {
                if (current == 1)
                {
                    var index = stack.FindLastIndex(item => item.Equals(node, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0)
                    {
                        var path = stack.Skip(index).Append(node).ToArray();
                        var key = string.Join("|", path.Take(path.Length - 1).OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
                        if (cycleKeys.Add(key)) cycles.Add(new DependencyCycle(path));
                    }
                }
                return;
            }
            state[node] = 1;
            stack.Add(node);
            if (adjacency.TryGetValue(node, out var targets)) foreach (var target in targets) Visit(target);
            stack.RemoveAt(stack.Count - 1);
            state[node] = 2;
        }
    }

    private readonly record struct EdgeKey(string SourceId, string TargetId, DependencyRelationshipType Relationship);
    private sealed class EdgeKeyComparer : IEqualityComparer<EdgeKey>
    {
        public static EdgeKeyComparer Instance { get; } = new();
        public bool Equals(EdgeKey x, EdgeKey y) =>
            string.Equals(x.SourceId, y.SourceId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.TargetId, y.TargetId, StringComparison.OrdinalIgnoreCase) &&
            x.Relationship == y.Relationship;
        public int GetHashCode(EdgeKey obj) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SourceId),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TargetId),
            obj.Relationship);
    }
}
