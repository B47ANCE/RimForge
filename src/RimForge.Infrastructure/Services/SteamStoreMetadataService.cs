using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class SteamStoreMetadataService : ISteamStoreMetadataService
{
    private sealed record OfficialApp(int AppId, string Name, string Description);

    private static readonly IReadOnlyDictionary<string, OfficialApp> OfficialApps =
        new Dictionary<string, OfficialApp>(StringComparer.OrdinalIgnoreCase)
        {
            ["ludeon.rimworld"] = new(294100, "RimWorld", "A sci-fi colony simulation driven by an intelligent AI storyteller."),
            ["ludeon.rimworld.royalty"] = new(1149640, "RimWorld - Royalty", "The Royalty expansion adds the Empire, royal titles, psychic powers, quests, and new technologies."),
            ["ludeon.rimworld.ideology"] = new(1392840, "RimWorld - Ideology", "The Ideology expansion lets colonies define belief systems, rituals, roles, relics, and social rules."),
            ["ludeon.rimworld.biotech"] = new(1826140, "RimWorld - Biotech", "The Biotech expansion adds children, genetic modification, mechanitors, and controllable mechanoids."),
            ["ludeon.rimworld.anomaly"] = new(2380740, "RimWorld - Anomaly", "The Anomaly expansion introduces mysterious entities, containment, research, and horror-themed events."),
            ["ludeon.rimworld.odyssey"] = new(3022790, "RimWorld - Odyssey", "The Odyssey expansion expands colony exploration with new journeys, environments, threats, and discoveries.")
        };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly HttpClient _httpClient;
    private readonly string _cacheRoot;

    public SteamStoreMetadataService(string cacheRoot, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RimForge/1.0");
        _cacheRoot = Path.GetFullPath(cacheRoot);
    }

    public bool TryGetOfficialAppId(string? packageId, out int appId)
    {
        if (TryGetOfficialApp(packageId, out var app))
        {
            appId = app.AppId;
            return true;
        }

        appId = 0;
        return false;
    }

    public SteamStoreMetadata? GetOfficialFallbackMetadata(string? packageId)
    {
        return TryGetOfficialApp(packageId, out var app)
            ? CreateFallbackMetadata(app)
            : null;
    }

    public async Task<SteamStoreMetadata?> GetOfficialMetadataAsync(
        string? packageId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetOfficialApp(packageId, out var officialApp))
        {
            return null;
        }

        Directory.CreateDirectory(_cacheRoot);
        var metadataPath = Path.Combine(_cacheRoot, $"store-{officialApp.AppId}.json");
        var cached = await ReadCacheAsync(metadataPath, cancellationToken).ConfigureAwait(false);
        var fallback = CreateFallbackMetadata(officialApp);
        if (cached is not null && IsFresh(metadataPath, TimeSpan.FromHours(24)))
        {
            return cached;
        }

        try
        {
            using var document = await _httpClient.GetFromJsonAsync<JsonDocument>(
                $"https://store.steampowered.com/api/appdetails?appids={officialApp.AppId}&l=english&cc=us",
                cancellationToken).ConfigureAwait(false);

            if (document is null ||
                !document.RootElement.TryGetProperty(officialApp.AppId.ToString(CultureInfo.InvariantCulture), out var envelope) ||
                !envelope.TryGetProperty("success", out var success) ||
                !success.GetBoolean() ||
                !envelope.TryGetProperty("data", out var data))
            {
                return cached ?? await CacheFallbackAsync(metadataPath, fallback, cancellationToken).ConfigureAwait(false);
            }

            var imageUrl = GetString(data, "header_image") ?? fallback.HeaderImageUrl;
            var imagePath = await TryCacheImageAsync($"store-{officialApp.AppId}", imageUrl, cancellationToken).ConfigureAwait(false);
            var metadata = new SteamStoreMetadata
            {
                AppId = officialApp.AppId,
                Name = GetString(data, "name") ?? fallback.Name,
                Description = GetString(data, "short_description") ?? fallback.Description,
                Developer = GetFirstArrayString(data, "developers"),
                Publisher = GetFirstArrayString(data, "publishers"),
                ReleaseDate = data.TryGetProperty("release_date", out var release)
                    ? GetString(release, "date") ?? string.Empty
                    : string.Empty,
                StoreUrl = fallback.StoreUrl,
                HeaderImageUrl = imageUrl,
                CachedHeaderImagePath = imagePath
            };

            await WriteCacheAsync(metadataPath, metadata, cancellationToken).ConfigureAwait(false);
            return metadata;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return cached ?? await CacheFallbackAsync(metadataPath, fallback, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<SteamStoreMetadata?> GetWorkshopMetadataAsync(
        string? workshopId,
        CancellationToken cancellationToken = default)
    {
        if (!ulong.TryParse(workshopId, NumberStyles.None, CultureInfo.InvariantCulture, out var publishedFileId))
        {
            return null;
        }

        Directory.CreateDirectory(_cacheRoot);
        var metadataPath = Path.Combine(_cacheRoot, $"workshop-{publishedFileId}.json");
        var cached = await ReadCacheAsync(metadataPath, cancellationToken).ConfigureAwait(false);
        if (IsFresh(metadataPath, TimeSpan.FromHours(6)))
        {
            return cached;
        }

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["itemcount"] = "1",
                ["publishedfileids[0]"] = publishedFileId.ToString(CultureInfo.InvariantCulture)
            });
            using var response = await _httpClient.PostAsync(
                "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                content,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
            if (!document.RootElement.TryGetProperty("response", out var responseNode) ||
                !responseNode.TryGetProperty("publishedfiledetails", out var details) ||
                details.ValueKind != JsonValueKind.Array ||
                details.GetArrayLength() == 0)
            {
                return cached;
            }

            var item = details[0];
            if (GetInt64(item, "result") is not 1)
            {
                return cached;
            }

            var previewUrl = GetString(item, "preview_url");
            var imagePath = await TryCacheImageAsync($"workshop-{publishedFileId}", previewUrl, cancellationToken).ConfigureAwait(false);
            var metadata = new SteamStoreMetadata
            {
                AppId = (int)(GetInt64(item, "consumer_app_id") ?? 294100),
                PublishedFileId = publishedFileId,
                Name = GetString(item, "title") ?? $"Workshop Item {publishedFileId}",
                Description = CleanWorkshopDescription(GetString(item, "description")),
                Creator = GetString(item, "creator") ?? string.Empty,
                ReleaseDate = FormatUnixTime(GetInt64(item, "time_created")),
                UpdatedDate = FormatUnixTime(GetInt64(item, "time_updated")),
                SubscriberCount = GetInt64(item, "subscriptions") ?? 0,
                StoreUrl = $"https://steamcommunity.com/sharedfiles/filedetails/?id={publishedFileId}",
                HeaderImageUrl = previewUrl,
                CachedHeaderImagePath = imagePath
            };

            await WriteCacheAsync(metadataPath, metadata, cancellationToken).ConfigureAwait(false);
            return metadata;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return cached;
        }
    }

    private static bool TryGetOfficialApp(string? packageId, out OfficialApp app) =>
        OfficialApps.TryGetValue(packageId?.Trim() ?? string.Empty, out app!);

    private static SteamStoreMetadata CreateFallbackMetadata(OfficialApp app)
    {
        var imageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{app.AppId}/header.jpg";
        return new SteamStoreMetadata
        {
            AppId = app.AppId,
            Name = app.Name,
            Description = app.Description,
            Developer = "Ludeon Studios",
            Publisher = "Ludeon Studios",
            StoreUrl = $"https://store.steampowered.com/app/{app.AppId}",
            HeaderImageUrl = imageUrl
        };
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? GetInt64(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return number;
        return null;
    }

    private static string GetFirstArrayString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var values) &&
        values.ValueKind == JsonValueKind.Array &&
        values.GetArrayLength() > 0
            ? values[0].GetString() ?? string.Empty
            : string.Empty;

    private static string FormatUnixTime(long? unixTime) =>
        unixTime is > 0
            ? DateTimeOffset.FromUnixTimeSeconds(unixTime.Value).ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture)
            : string.Empty;

    private static string CleanWorkshopDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;
        var withoutBbCode = Regex.Replace(description, @"\[/?[^\]]+\]", string.Empty);
        var normalized = Regex.Replace(withoutBbCode, @"\s+", " ").Trim();
        return normalized.Length <= 700 ? normalized : normalized[..697] + "…";
    }

    private static bool IsFresh(string path, TimeSpan maxAge) =>
        File.Exists(path) && File.GetLastWriteTimeUtc(path) > DateTime.UtcNow.Subtract(maxAge);

    private static async Task<SteamStoreMetadata?> ReadCacheAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<SteamStoreMetadata>(
                await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
                JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteCacheAsync(string path, SteamStoreMetadata metadata, CancellationToken cancellationToken)
    {
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(metadata, JsonOptions),
            cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, path, true);
    }

    private static async Task<SteamStoreMetadata> CacheFallbackAsync(
        string path,
        SteamStoreMetadata fallback,
        CancellationToken cancellationToken)
    {
        await WriteCacheAsync(path, fallback, cancellationToken).ConfigureAwait(false);
        return fallback;
    }

    private async Task<string?> TryCacheImageAsync(string cacheKey, string? imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        try
        {
            var extension = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";
            var path = Path.Combine(_cacheRoot, $"{cacheKey}-preview{extension}");
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) > DateTime.UtcNow.AddDays(-7))
            {
                return path;
            }

            var bytes = await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
            return path;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
