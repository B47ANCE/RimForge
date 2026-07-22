namespace RimForge.Core.Models;

public sealed record SteamInstallationCandidate(
    string LibraryRoot,
    string WorkshopFolder,
    string LocalModsFolder,
    string? GameExecutable = null,
    string? SteamExecutable = null)
{
    public string DisplayName => $"{LibraryRoot}  •  RimWorld";
    public bool CanLaunchDirectly => !string.IsNullOrWhiteSpace(GameExecutable) && File.Exists(GameExecutable);

    public string OfficialContentFolder
    {
        get
        {
            var gameRoot = Directory.GetParent(LocalModsFolder.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar))?.FullName;
            return gameRoot is null ? string.Empty : Path.Combine(gameRoot, "Data");
        }
    }
}
