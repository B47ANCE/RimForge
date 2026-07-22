using RimForge.Core.Models;

namespace RimForge.Core.Services;

public interface ISteamStoreMetadataService
{
    bool TryGetOfficialAppId(string? packageId, out int appId);
    SteamStoreMetadata? GetOfficialFallbackMetadata(string? packageId);
    Task<SteamStoreMetadata?> GetOfficialMetadataAsync(string? packageId, CancellationToken cancellationToken = default);
    Task<SteamStoreMetadata?> GetWorkshopMetadataAsync(string? workshopId, CancellationToken cancellationToken = default);
}
