using System.Text.Json;
using RimForge.Core.Models;

namespace RimForge.Infrastructure.Services;

public sealed class ProfileWorkspacePreferencesStore
{
    private const string FileName = "WorkspacePreferences.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<ProfileWorkspacePreferences> LoadAsync(
        RimForgeProfile profile,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(profile.WorkspacePath, FileName);
        if (!File.Exists(path)) return ProfileWorkspacePreferences.Empty;
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<ProfileWorkspacePreferences>(stream, JsonOptions, cancellationToken)
                   ?? ProfileWorkspacePreferences.Empty;
        }
        catch (JsonException)
        {
            return ProfileWorkspacePreferences.Empty;
        }
    }

    public async Task SaveAsync(
        RimForgeProfile profile,
        ProfileWorkspacePreferences preferences,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(profile.WorkspacePath);
        var path = Path.Combine(profile.WorkspacePath, FileName);
        var staged = path + ".tmp";
        var normalized = preferences with
        {
            LockedPositions = preferences.LockedPositions
                .Where(item => !string.IsNullOrWhiteSpace(item.PackageId))
                .DistinctBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item.Position)
                .ToArray(),
            DismissedRecommendationIds = preferences.DismissedRecommendationIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ExpandedExplanationPackageIds = preferences.ExpandedExplanationPackageIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        await File.WriteAllTextAsync(staged, JsonSerializer.Serialize(normalized, JsonOptions), cancellationToken);
        File.Move(staged, path, true);
    }
}
