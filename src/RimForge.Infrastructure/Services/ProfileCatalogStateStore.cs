using System.Text.Json;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class ProfileCatalogStateStore : IProfileCatalogStateStore
{
    private const string FileName = "ProfileCatalogState.json";
    private const string LegacyFileName = "ProfileShellState.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ProfileCatalogState Load(string profilesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilesRoot);
        var path = Path.Combine(profilesRoot, FileName);
        if (File.Exists(path)) return ReadCurrent(path);

        var legacyPath = Path.Combine(profilesRoot, LegacyFileName);
        if (!File.Exists(legacyPath)) return ProfileCatalogState.Empty;
        var migrated = ReadLegacy(legacyPath);
        Save(profilesRoot, migrated);
        return migrated;
    }

    public ProfileCatalogState Save(string profilesRoot, ProfileCatalogState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilesRoot);
        ArgumentNullException.ThrowIfNull(state);
        Directory.CreateDirectory(profilesRoot);
        var normalized = state with
        {
            FavoriteProfileNames = Normalize(state.FavoriteProfileNames),
            LockedProfileNames = Normalize(state.LockedProfileNames),
            UpdatedUtc = DateTimeOffset.UtcNow
        };
        var path = Path.Combine(profilesRoot, FileName);
        var stagedPath = path + ".tmp";
        File.WriteAllText(stagedPath, JsonSerializer.Serialize(normalized, JsonOptions));
        File.Move(stagedPath, path, true);
        return normalized;
    }

    private static ProfileCatalogState ReadCurrent(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<ProfileCatalogState>(File.ReadAllText(path), JsonOptions)
                ?? ProfileCatalogState.Empty;
        }
        catch (JsonException)
        {
            return ProfileCatalogState.Empty;
        }
    }

    private static ProfileCatalogState ReadLegacy(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return new ProfileCatalogState(
                ReadNames(document.RootElement, "favorites"),
                ReadNames(document.RootElement, "locked"),
                DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            return ProfileCatalogState.Empty;
        }
    }

    private static IReadOnlyList<string> ReadNames(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var values) && values.ValueKind == JsonValueKind.Array
            ? Normalize(values.EnumerateArray().Select(value => value.GetString() ?? string.Empty))
            : Array.Empty<string>();

    private static string[] Normalize(IEnumerable<string> values) => values
        .Select(value => value.Trim())
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
