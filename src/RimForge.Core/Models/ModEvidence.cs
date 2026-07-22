namespace RimForge.Core.Models;

public enum ModEvidenceKind
{
    CSharp,
    Xml,
    Definitions,
    Harmony,
    Textures,
    Audio,
    Languages,
    Assemblies,
    PatchOperations,
    Scenarios,
    Biomes,
    Factions,
    Research,
    Recipes,
    Buildings,
    Apparel,
    Weapons,
    Animals,
    Plants,
    PawnKinds,
    Incidents,
    Hediffs,
    Jobs,
    ArtificialIntelligence,
    WorldGeneration,
    SaveData
}

public sealed record ModEvidenceBadge(
    ModEvidenceKind Kind,
    string Label,
    int Count,
    string Summary,
    string Details,
    IReadOnlyList<string>? Files = null)
{
    public string CountText => Count > 0 ? Count.ToString() : string.Empty;
    public IReadOnlyList<string> FileList => Files ?? Array.Empty<string>();
    public bool HasTechnologyExplanation => Kind is ModEvidenceKind.CSharp or ModEvidenceKind.Harmony or ModEvidenceKind.Xml or ModEvidenceKind.PatchOperations;
    public string PlainLanguageExplanation => Kind switch
    {
        ModEvidenceKind.CSharp => "This mod contains compiled .NET code, allowing it to add gameplay logic, systems, user interfaces, AI, and other behavior beyond XML definitions.",
        ModEvidenceKind.Harmony => "This mod alters existing game code at runtime rather than only adding new content.",
        ModEvidenceKind.Xml => "This mod adds or changes RimWorld definitions used for content such as items, pawns, recipes, research, buildings, and other game objects.",
        ModEvidenceKind.PatchOperations => "This mod changes existing XML definitions while the game is loading instead of replacing the original files.",
        _ => string.Empty
    };
    public string WhyItMatters => Kind switch
    {
        ModEvidenceKind.CSharp => "Compiled code can introduce complex behavior and may interact with the game or other mods in ways that data-only content cannot.",
        ModEvidenceKind.Harmony => "Runtime patches can change vanilla or modded behavior, interact with other patches, affect performance, and participate in method-level conflicts.",
        ModEvidenceKind.Xml => "XML-driven content is generally data-focused, making it easier to inspect and reason about than executable runtime code.",
        ModEvidenceKind.PatchOperations => "Patch order and target definitions matter because multiple mods may attempt to change the same XML content during loading.",
        _ => string.Empty
    };
    public string SearchText => $"{Kind} {Label} {PlainLanguageExplanation} {WhyItMatters} {Summary} {Details} {string.Join(" ", FileList)}";
}

public sealed class ModEvidence
{
    public static ModEvidence Empty { get; } = new();

    public int TotalFiles { get; init; }
    public long TotalBytes { get; init; }
    public int XmlFiles { get; init; }
    public int AssemblyFiles { get; init; }
    public int TextureFiles { get; init; }
    public int AudioFiles { get; init; }
    public int LanguageFiles { get; init; }
    public int DefinitionCount { get; init; }
    public int PatchOperationCount { get; init; }
    public int HarmonyHintCount { get; init; }
    public IReadOnlyList<ModEvidenceBadge> Badges { get; init; } = Array.Empty<ModEvidenceBadge>();
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NotableFindings { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ModEvidenceBadge> VisibleBadges
    {
        get
        {
            return Badges
                .OrderBy(badge => BadgeDisplayPriority(badge.Kind))
                .ThenByDescending(badge => badge.Count)
                .ThenBy(badge => badge.Label, StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToArray();
        }
    }
    public int HiddenBadgeCount => Math.Max(0, Badges.Count - VisibleBadges.Count);
    public string HiddenBadgeText => HiddenBadgeCount == 0 ? string.Empty : $"+{HiddenBadgeCount}";
    public bool HasHiddenBadges => HiddenBadgeCount > 0;
    public string DnaText => Badges.Count == 0 ? "No evidence discovered" : string.Join(" · ", Badges.Select(badge => badge.Label));
    public string CapabilityText => Capabilities.Count == 0 ? "No high-confidence capabilities inferred." : string.Join(Environment.NewLine, Capabilities.Select(item => $"• {item}"));
    public string InventorySummary => $"{TotalFiles:N0} files · {FormatBytes(TotalBytes)}";
    public string SearchText => string.Join(" ", Badges.Select(badge => badge.SearchText).Concat(Capabilities).Concat(NotableFindings));

    private static int BadgeDisplayPriority(ModEvidenceKind kind) => kind switch
    {
        ModEvidenceKind.CSharp => 0,
        ModEvidenceKind.Harmony => 1,
        ModEvidenceKind.Xml => 2,
        ModEvidenceKind.PatchOperations => 3,
        ModEvidenceKind.Factions or ModEvidenceKind.Weapons or ModEvidenceKind.Apparel or ModEvidenceKind.Animals or
        ModEvidenceKind.Plants or ModEvidenceKind.Buildings or ModEvidenceKind.Research or ModEvidenceKind.WorldGeneration or
        ModEvidenceKind.Scenarios or ModEvidenceKind.ArtificialIntelligence or ModEvidenceKind.Hediffs or ModEvidenceKind.Jobs or
        ModEvidenceKind.Incidents or ModEvidenceKind.PawnKinds or ModEvidenceKind.Biomes or ModEvidenceKind.Recipes => 10,
        ModEvidenceKind.Definitions or ModEvidenceKind.Textures or ModEvidenceKind.Audio or ModEvidenceKind.Languages or
        ModEvidenceKind.Assemblies or ModEvidenceKind.SaveData => 20,
        _ => 30
    };

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }
        return $"{value:0.#} {units[index]}";
    }
}
