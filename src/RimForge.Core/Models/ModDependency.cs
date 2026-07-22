namespace RimForge.Core.Models;

public sealed record ModDependency(
    string PackageId,
    string? DisplayName,
    string? SteamWorkshopUrl,
    string? DownloadUrl,
    string Source,
    string? RimWorldVersion = null);
