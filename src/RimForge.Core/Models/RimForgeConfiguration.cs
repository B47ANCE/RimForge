namespace RimForge.Core.Models;

public sealed class RimForgeConfiguration
{
    public string Version { get; init; } = "development";
    public IReadOnlyList<string> RootFolders { get; init; } = Array.Empty<string>();
    public string TargetRimWorldVersion { get; init; } = "1.6";
    public string OutputFolder { get; init; } = "Output";
    public string? RimWorldExecutable { get; init; }
    public string? SteamExecutable { get; init; }
}
