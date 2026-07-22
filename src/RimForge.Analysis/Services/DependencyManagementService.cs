using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Analysis.Services;

public sealed class DependencyManagementService : IDependencyManagementService
{
    private readonly IDependencyIntelligenceService _intelligence;

    public DependencyManagementService(IDependencyIntelligenceService intelligence) => _intelligence = intelligence;

    public DependencyActivationPlan PlanActivation(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyCollection<string> activePackageIds,
        IReadOnlyCollection<string> requestedPackageIds)
    {
        var mods = BuildModMap(installedMods);
        var active = activePackageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requested = requestedPackageIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var ordered = new List<string>();
        var missing = new Dictionary<string, MissingDependencyRequirement>(StringComparer.OrdinalIgnoreCase);
        var cycles = new List<IReadOnlyList<string>>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new List<string>();

        void Visit(string packageId, string requiredBy, bool traverseActiveRoot = false)
        {
            if ((!traverseActiveRoot && active.Contains(packageId)) || visited.Contains(packageId)) return;
            var cycleIndex = visiting.FindIndex(id => id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            if (cycleIndex >= 0)
            {
                cycles.Add(visiting.Skip(cycleIndex).Append(packageId).ToArray());
                return;
            }
            if (!mods.TryGetValue(packageId, out var mod)) return;

            visiting.Add(packageId);
            foreach (var dependency in mod.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(dependency.PackageId) || active.Contains(dependency.PackageId)) continue;
                if (!mods.ContainsKey(dependency.PackageId))
                {
                    var path = visiting.Append(dependency.PackageId).ToArray();
                    if (missing.TryGetValue(dependency.PackageId, out var existing))
                    {
                        missing[dependency.PackageId] = existing with
                        {
                            RequiredByPackageIds = existing.RequiredByPackageIds.Append(requiredBy)
                                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                        };
                    }
                    else
                    {
                        missing[dependency.PackageId] = new MissingDependencyRequirement(
                            dependency.PackageId,
                            dependency.DisplayName ?? dependency.PackageId,
                            new[] { requiredBy },
                            path);
                    }
                    continue;
                }
                Visit(dependency.PackageId, requiredBy);
                if (!active.Contains(dependency.PackageId) && visited.Add(dependency.PackageId))
                {
                    ordered.Add(dependency.PackageId);
                    active.Add(dependency.PackageId);
                }
            }
            visiting.RemoveAt(visiting.Count - 1);
        }

        foreach (var packageId in requested)
            Visit(packageId, packageId, traverseActiveRoot: true);

        return new DependencyActivationPlan(requested, ordered, missing.Values
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray(), cycles);
    }

    public DependencyRemovalPlan PlanRemoval(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyCollection<string> activePackageIds,
        IReadOnlyCollection<string> requestedPackageIds)
    {
        var requested = requestedPackageIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var requestedSet = requested.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var active = activePackageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var impacts = requested
            .SelectMany(id => _intelligence.Analyze(installedMods, active, id).RemovalImpact)
            .Where(reason => active.Contains(reason.PackageId) && !requestedSet.Contains(reason.PackageId))
            .GroupBy(reason => reason.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(reason => reason.Path.Count).First())
            .OrderBy(reason => reason.Path.Count)
            .ThenBy(reason => reason.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var cascade = requested.Concat(impacts.Select(reason => reason.PackageId))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var remaining = active.Except(cascade, StringComparer.OrdinalIgnoreCase).ToArray();
        var orphans = FindOrphans(installedMods, remaining);
        return new DependencyRemovalPlan(requested, impacts, cascade, orphans);
    }

    public IReadOnlyList<DependencyReason> FindOrphans(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyCollection<string> activePackageIds)
    {
        var active = activePackageIds.ToArray();
        if (active.Length == 0) return Array.Empty<DependencyReason>();
        return _intelligence.Analyze(installedMods, active, active[0]).OrphanCandidates;
    }

    public DependencyManagementSummary Summarize(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyCollection<string> activePackageIds)
    {
        var map = BuildModMap(installedMods);
        var active = activePackageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = active
            .Where(map.ContainsKey)
            .SelectMany(id => map[id].Dependencies.Select(dependency => (Owner: id, Dependency: dependency)))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Dependency.PackageId) && !active.Contains(pair.Dependency.PackageId))
            .Select(pair => pair.Dependency.PackageId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var orphans = FindOrphans(installedMods, activePackageIds);
        var dependencyBearing = activePackageIds.Count(id => map.TryGetValue(id, out var mod) && mod.Dependencies.Count > 0);
        var healthy = missing == 0;
        var summary = healthy
            ? $"Dependency closure is complete · {orphans.Count} orphan candidate(s)"
            : $"{missing} missing dependency requirement(s) · {orphans.Count} orphan candidate(s)";
        return new DependencyManagementSummary(activePackageIds.Count, missing,
            orphans.Count, dependencyBearing, healthy, summary);
    }

    private static Dictionary<string, ModRecord> BuildModMap(IEnumerable<ModRecord> installedMods) => installedMods
        .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
        .Select(ApplyCanonicalDependencies)
        .GroupBy(mod => mod.PackageId!, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    private static ModRecord ApplyCanonicalDependencies(ModRecord mod)
    {
        var canonical = LoadOrderRules.GetCanonicalDependencies(mod.PackageId);
        if (canonical.Count == 0 || mod.Dependencies.Any(dependency =>
                dependency.PackageId.Equals(LoadOrderRules.CorePackageId, StringComparison.OrdinalIgnoreCase)))
            return mod;

        return new ModRecord
        {
            Id = mod.Id,
            RootPath = mod.RootPath,
            FolderName = mod.FolderName,
            AboutPath = mod.AboutPath,
            Name = mod.Name,
            PackageId = mod.PackageId,
            Author = mod.Author,
            Description = mod.Description,
            WorkshopId = mod.WorkshopId,
            WorkshopUrl = mod.WorkshopUrl,
            PreviewImagePath = mod.PreviewImagePath,
            LastModified = mod.LastModified,
            SupportedVersions = mod.SupportedVersions,
            Dependencies = mod.Dependencies.Concat(canonical).ToArray(),
            LoadBefore = mod.LoadBefore,
            LoadAfter = mod.LoadAfter,
            IncompatibleWith = mod.IncompatibleWith,
            Errors = mod.Errors,
            Evidence = mod.Evidence
        };
    }
}
