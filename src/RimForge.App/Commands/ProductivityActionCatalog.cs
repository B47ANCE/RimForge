namespace RimForge.App.Commands;

public sealed record ProductivityActionDescriptor(string Id, string Title, string Description, string Glyph, string? Destination, params string[] Aliases);

public static class ProductivityActionCatalog
{
    public static IReadOnlyList<ProductivityActionDescriptor> All { get; } =
    [
        new("navigate.mod-sorter", "Mod Sorter", "Manage active and inactive mods", "\uE8CB", "Mod Sorter", "mods", "load order", "sorting", "dashboard"),
        new("navigate.issue-viewer", "Issue Viewer", "Review health findings and repairs", "\uE7BA", "Issue Viewer", "issues", "diagnostics", "health", "anvil", "repairs"),
        new("navigate.forge-view", "ForgeView", "Explore dependency and incompatibility relationships", "\uE9D2", "ForgeView", "graph", "dependencies", "relationships", "conflicts"),
        new("navigate.texture-tools", "Texture Conversion Tools", "Analyze and convert textures to BC7 DDS", "\uE790", "Texture Tools", "texture", "textures", "dds", "bc7", "converter"),
        new("navigate.console", "Console", "Inspect RimForge activity and game logs", "\uE756", "Console", "log", "logs", "activity", "player.log"),
        new("navigate.settings", "Settings", "Configure profiles, paths, launch, and behavior", "\uE713", "Settings", "preferences", "profiles", "paths", "launch", "configuration"),
        new("profile.enable-selected", "Enable selected mods", "Preview and add selected library mods to the active profile", "\uE710", null, "bulk enable", "activate selected", "add mods"),
        new("profile.disable-selected", "Disable selected mods", "Preview and remove selected mods from the active profile", "\uE711", null, "bulk disable", "deactivate selected", "remove mods")
    ];
}
