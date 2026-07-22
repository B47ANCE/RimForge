using System.Text.Json;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class FeatureFlagService : IFeatureFlagService
{
    public FeatureFlags Current { get; private set; } = new();

    public async Task LoadAsync(string repositoryRoot, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(repositoryRoot, "Features.json");
        if (!File.Exists(path)) return;
        await using var stream = File.OpenRead(path);
        Current = await JsonSerializer.DeserializeAsync<FeatureFlags>(stream, cancellationToken: cancellationToken) ?? new FeatureFlags();
    }
}
