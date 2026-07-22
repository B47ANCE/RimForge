namespace RimForge.Core.Models;

public static class LoadOrderRules
{
    public const string HarmonyPackageId = "brrainz.harmony";
    public const string CorePackageId = "ludeon.rimworld";
    public const string RocketManPackageId = "krkr.rocketman";
    public const string MissileGirlPackageId = "vr.missilegirl";
    public const string SrtsPackageId = "smashphil.srtsexpanded";
    public const string HugsLibPackageId = "unlimitedhugs.hugslib";
    public const string JecsToolsPackageId = "jecrell.jecstools";
    public const string HumanoidAlienRacesPackageId = "erdelf.humanoidalienraces";
    public const string PrepareCarefullyPackageId = "edb.preparecarefully";
    public const string CharacterEditorPackageId = "void.charactereditor";

    public static IReadOnlyList<string> OfficialDlcOrder { get; } =
    [
        "ludeon.rimworld.royalty",
        "ludeon.rimworld.ideology",
        "ludeon.rimworld.biotech",
        "ludeon.rimworld.anomaly",
        "ludeon.rimworld.odyssey"
    ];

    public static IReadOnlyList<string> TopAnchors { get; } =
    [
        HarmonyPackageId,
        CorePackageId,
        .. OfficialDlcOrder,
        SrtsPackageId,
        HugsLibPackageId,
        JecsToolsPackageId,
        HumanoidAlienRacesPackageId,
        PrepareCarefullyPackageId,
        CharacterEditorPackageId
    ];

    public static IReadOnlyList<string> BottomAnchors { get; } =
        [RocketManPackageId, MissileGirlPackageId];

    public static bool IsTopAnchor(string? packageId) =>
        packageId is not null && TopAnchors.Contains(packageId, StringComparer.OrdinalIgnoreCase);

    public static bool IsBottomAnchor(string? packageId) =>
        packageId is not null && BottomAnchors.Contains(packageId, StringComparer.OrdinalIgnoreCase);

    // Position anchors have canonical placement while active, but are not necessarily mandatory.
    public static bool IsPositionAnchor(string? packageId) => IsTopAnchor(packageId) || IsBottomAnchor(packageId);

    // Backward-compatible alias used by ordering code.
    public static bool IsAnchor(string? packageId) => IsPositionAnchor(packageId);

    // Core is the only mod that must always remain active.
    public static bool IsMandatory(string? packageId) => IsCore(packageId);

    public static bool CanDeactivate(string? packageId) => !IsMandatory(packageId);

    public static bool IsCore(string? packageId) =>
        packageId is not null && packageId.Equals(CorePackageId, StringComparison.OrdinalIgnoreCase);

    public static bool IsOfficialDlc(string? packageId) =>
        packageId is not null && OfficialDlcOrder.Contains(packageId, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ModDependency> GetCanonicalDependencies(string? packageId) =>
        IsOfficialDlc(packageId)
            ? [new ModDependency(CorePackageId, "Core", null, null, "RimForge canonical official-content rule")]
            : Array.Empty<ModDependency>();

    public static IReadOnlyList<string> Normalize(IEnumerable<string> packageIds)
    {
        var unique = packageIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!unique.Contains(CorePackageId, StringComparer.OrdinalIgnoreCase)) unique.Insert(0, CorePackageId);

        var set = unique.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(unique.Count);

        foreach (var anchor in TopAnchors)
            if (set.Contains(anchor)) result.Add(anchor);

        foreach (var packageId in unique)
            if (!IsAnchor(packageId)) result.Add(packageId);

        foreach (var anchor in BottomAnchors)
            if (set.Contains(anchor)) result.Add(anchor);

        return result;
    }
}
