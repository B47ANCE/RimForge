using RimForge.Core.Models;

namespace RimForge.Analysis.Services;

public sealed record LockedPositionResult(
    bool Success,
    IReadOnlyList<string> OrderedPackageIds,
    IReadOnlyList<LoadOrderLockConflict> Conflicts);

public static class LockedPositionSolver
{
    public static LockedPositionResult Apply(
        IReadOnlyList<string> topologicalOrder,
        IReadOnlyDictionary<string, List<string>> hardAdjacency,
        IReadOnlyList<UserLoadOrderLock>? locks)
    {
        if (locks is null || locks.Count == 0)
            return new(true, topologicalOrder.ToArray(), Array.Empty<LoadOrderLockConflict>());

        var order = topologicalOrder.ToList();
        var conflicts = new List<LoadOrderLockConflict>();
        foreach (var item in locks
                     .Where(item => order.Contains(item.PackageId, StringComparer.OrdinalIgnoreCase))
                     .OrderBy(item => item.Position)
                     .ThenBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            var packageIndex = order.FindIndex(id => id.Equals(item.PackageId, StringComparison.OrdinalIgnoreCase));
            if (packageIndex < 0) continue;
            var requested = item.NormalizedPosition(order.Count);
            order.RemoveAt(packageIndex);
            order.Insert(requested, item.PackageId);

            var violation = FindViolation(order, hardAdjacency, item.PackageId);
            if (violation is null) continue;

            order.RemoveAt(requested);
            order.Insert(Math.Min(packageIndex, order.Count), item.PackageId);
            var legal = FindLegalPositions(order, hardAdjacency, item.PackageId);
            conflicts.Add(new LoadOrderLockConflict(
                item.PackageId,
                requested,
                violation,
                $"The requested lock would place {item.PackageId} across the required ordering constraint involving {violation}.",
                legal.Count > 0,
                legal));
        }

        return new(conflicts.Count == 0, order, conflicts);
    }

    private static string? FindViolation(
        IReadOnlyList<string> order,
        IReadOnlyDictionary<string, List<string>> adjacency,
        string lockedPackageId)
    {
        var positions = order.Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index, StringComparer.OrdinalIgnoreCase);
        foreach (var (before, afterValues) in adjacency)
        {
            foreach (var after in afterValues)
            {
                if (positions[before] < positions[after]) continue;
                if (before.Equals(lockedPackageId, StringComparison.OrdinalIgnoreCase)) return after;
                if (after.Equals(lockedPackageId, StringComparison.OrdinalIgnoreCase)) return before;
            }
        }
        return null;
    }

    private static IReadOnlyList<int> FindLegalPositions(
        IReadOnlyList<string> currentOrder,
        IReadOnlyDictionary<string, List<string>> adjacency,
        string packageId)
    {
        var without = currentOrder.Where(id => !id.Equals(packageId, StringComparison.OrdinalIgnoreCase)).ToList();
        var legal = new List<int>();
        for (var position = 0; position <= without.Count; position++)
        {
            var candidate = without.ToList();
            candidate.Insert(position, packageId);
            if (FindViolation(candidate, adjacency, packageId) is null) legal.Add(position);
        }
        return legal;
    }
}
