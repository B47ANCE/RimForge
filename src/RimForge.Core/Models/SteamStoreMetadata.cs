namespace RimForge.Core.Models;

public sealed class SteamStoreMetadata
{
    public required int AppId { get; init; }
    public ulong? PublishedFileId { get; init; }
    public bool IsWorkshopItem => PublishedFileId.HasValue;
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Developer { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string Creator { get; init; } = string.Empty;
    public string ReleaseDate { get; init; } = string.Empty;
    public string UpdatedDate { get; init; } = string.Empty;
    public long SubscriberCount { get; init; }
    public required string StoreUrl { get; init; }
    public string? HeaderImageUrl { get; init; }
    public string? CachedHeaderImagePath { get; init; }
    public string? DisplayImagePath => !string.IsNullOrWhiteSpace(CachedHeaderImagePath) ? CachedHeaderImagePath : HeaderImageUrl;
    public string SectionTitle => IsWorkshopItem ? "STEAM WORKSHOP" : "STEAM STORE";
    public string CreatorLabel => IsWorkshopItem ? "Creator" : "Developer";
    public string CreatorDisplay => IsWorkshopItem
        ? (!string.IsNullOrWhiteSpace(Creator) ? Creator : "Unknown")
        : (!string.IsNullOrWhiteSpace(Developer) ? Developer : "Unknown");
    public string DateLabel => IsWorkshopItem ? "Updated" : "Released";
    public string DateDisplay => IsWorkshopItem
        ? (!string.IsNullOrWhiteSpace(UpdatedDate) ? UpdatedDate : "Unknown")
        : (!string.IsNullOrWhiteSpace(ReleaseDate) ? ReleaseDate : "Unknown");
    public string OpenPageLabel => IsWorkshopItem ? "Open Steam Workshop Page" : "Open Steam Store Page";
}
