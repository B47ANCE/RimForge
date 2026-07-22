namespace RimForge.Core.Models;

public enum LoadOrderSortingMode
{
    CategoryFirst,
    DependencyFirst,
    PreserveManual
}

public sealed record SortingPolicySettings(
    LoadOrderSortingMode Mode,
    bool LaterBandsOverrideEarlier,
    bool ReconcileDependenciesDownwardOnly,
    bool CuratedAnchorsBidirectional,
    string RulePackId)
{
    public static SortingPolicySettings Default { get; } = new(
        LoadOrderSortingMode.CategoryFirst, true, true, true, "rimforge.category-first.v1");
}
