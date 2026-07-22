using RimForge.Core.Models;

namespace RimForge.Core.Services;

public interface IFeatureFlagService
{
    FeatureFlags Current { get; }
    Task LoadAsync(string repositoryRoot, CancellationToken cancellationToken = default);
}
