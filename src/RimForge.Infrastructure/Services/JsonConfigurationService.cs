using System.Text.Json;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class JsonConfigurationService : IConfigurationService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<RimForgeConfiguration> LoadAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(repositoryRoot, "Config.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("RimForge Config.json was not found.", path);
        }

        await using var stream = File.OpenRead(path);
        var configuration = await JsonSerializer.DeserializeAsync<RimForgeConfiguration>(
            stream,
            Options,
            cancellationToken).ConfigureAwait(false);

        if (configuration is null)
        {
            throw new InvalidDataException("Config.json did not contain a valid RimForge configuration.");
        }

        try
        {
            RimForgePathLayout.Create(repositoryRoot, configuration.OutputFolder).EnsureGeneratedDirectories();
        }
        catch
        {
            // Generated storage is recoverable. A directory creation failure should be
            // surfaced by the feature that needs it, not abort mod discovery.
        }
        return configuration;
    }
}
