using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Analysis.Services;

public sealed class DependencyIntelligenceService : IDependencyIntelligenceService
{
    public DependencyIntelligenceReport Analyze(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyCollection<string> activePackageIds,
        string? selectedPackageId)
    {
        if (string.IsNullOrWhiteSpace(selectedPackageId)) return DependencyIntelligenceReport.Empty();

        var mods = installedMods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
            .GroupBy(mod => mod.PackageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        if (!mods.TryGetValue(selectedPackageId, out var selected)) return DependencyIntelligenceReport.Empty(selectedPackageId);

        var active = activePackageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var outgoing = mods.Values.ToDictionary(
            mod => mod.PackageId!,
            mod => mod.Dependencies.Select(dependency => dependency.PackageId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var incoming = mods.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var (source, targets) in outgoing)
            foreach (var target in targets)
                if (incoming.TryGetValue(target, out var sources)) sources.Add(source);

        var directDependencies = BuildDirect(selected.PackageId!, outgoing, mods, "Directly required by the selected mod.");
        var directDependents = BuildDirect(selected.PackageId!, incoming.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase), mods, "Directly requires the selected mod.");
        var transitiveDependencies = Traverse(selected.PackageId!, outgoing, mods, "Required through the dependency chain");
        var transitiveDependents = Traverse(selected.PackageId!, incoming.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase), mods, "Relies on the selected mod through the dependency chain");

        var removalImpact = transitiveDependents
            .Where(reason => active.Contains(reason.PackageId))
            .OrderBy(reason => reason.Path.Count)
            .ThenBy(reason => reason.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var orphanCandidates = active
            .Where(id => mods.ContainsKey(id) && !ModRecord.IsOfficialRimWorldPackageId(id))
            .Where(id => incoming.TryGetValue(id, out var sources) && !sources.Any(active.Contains))
            .Where(id => outgoing.TryGetValue(id, out var dependencies) && dependencies.Length > 0)
            .Select(id => CreateReason(id, mods, new[] { id }, "No active mod currently requires this dependency-bearing mod.", 90))
            .OrderBy(reason => reason.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var isOrphan = active.Contains(selected.PackageId!)
            && !selected.IsOfficialContent
            && incoming.TryGetValue(selected.PackageId!, out var selectedIncoming)
            && !selectedIncoming.Any(active.Contains)
            && selected.Dependencies.Count > 0;

        var whyEnabled = directDependents.Count > 0
            ? $"Required directly by {string.Join(", ", directDependents.Where(reason => active.Contains(reason.PackageId)).Select(reason => reason.DisplayName).DefaultIfEmpty("no active mods"))}."
            : isOrphan ? "No active mod requires this mod; it may have been selected manually or may now be orphaned." : "Selected directly by the user or imported profile.";
        var removalExplanation = removalImpact.Length == 0
            ? "No active dependents would be disabled by removing this mod."
            : $"Removing this mod affects {removalImpact.Length} active mod(s): {string.Join(", ", removalImpact.Take(5).Select(reason => reason.DisplayName))}{(removalImpact.Length > 5 ? $" and {removalImpact.Length - 5} more" : string.Empty)}.";
        var confidence = selected.Dependencies.Count > 0 || directDependents.Count > 0 ? 98 : isOrphan ? 90 : 70;
        var summary = $"{transitiveDependencies.Count} dependencies · {transitiveDependents.Count} dependents · {removalImpact.Length} active removal impacts";

        return new DependencyIntelligenceReport(
            selected.PackageId!, directDependencies, transitiveDependencies, directDependents, transitiveDependents,
            removalImpact, orphanCandidates, isOrphan, confidence, summary, whyEnabled, removalExplanation);
    }

    private static IReadOnlyList<DependencyReason> BuildDirect(
        string root,
        IReadOnlyDictionary<string, string[]> adjacency,
        IReadOnlyDictionary<string, ModRecord> mods,
        string explanation) => adjacency.TryGetValue(root, out var ids)
            ? ids.Where(mods.ContainsKey).Select(id => CreateReason(id, mods, new[] { root, id }, explanation, 100))
                .OrderBy(reason => reason.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<DependencyReason>();

    private static IReadOnlyList<DependencyReason> Traverse(
        string root,
        IReadOnlyDictionary<string, string[]> adjacency,
        IReadOnlyDictionary<string, ModRecord> mods,
        string explanation)
    {
        var found = new Dictionary<string, DependencyReason>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<IReadOnlyList<string>>();
        queue.Enqueue(new[] { root });
        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var current = path[^1];
            if (!adjacency.TryGetValue(current, out var next)) continue;
            foreach (var id in next)
            {
                if (path.Contains(id, StringComparer.OrdinalIgnoreCase)) continue;
                var nextPath = path.Append(id).ToArray();
                if (mods.ContainsKey(id) && !found.ContainsKey(id))
                    found[id] = CreateReason(id, mods, nextPath, $"{explanation}: {FormatPath(nextPath, mods)}.", 100);
                queue.Enqueue(nextPath);
            }
        }
        return found.Values.OrderBy(reason => reason.Path.Count).ThenBy(reason => reason.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static DependencyReason CreateReason(string id, IReadOnlyDictionary<string, ModRecord> mods, IReadOnlyList<string> path, string explanation, int confidence)
    {
        var name = mods.TryGetValue(id, out var mod) ? mod.DisplayName : id;
        var level = confidence >= 98 ? DependencyConfidenceLevel.Verified : confidence >= 85 ? DependencyConfidenceLevel.High : confidence >= 65 ? DependencyConfidenceLevel.Medium : DependencyConfidenceLevel.Low;
        return new DependencyReason(id, name, path, explanation, level, confidence);
    }

    private static string FormatPath(IEnumerable<string> path, IReadOnlyDictionary<string, ModRecord> mods) =>
        string.Join(" → ", path.Select(id => mods.TryGetValue(id, out var mod) ? mod.DisplayName : id));
}
