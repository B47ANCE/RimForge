using RimForge.Core.Models;

namespace RimForge.Core.Services;

public sealed class ProfileWorkspaceStateService : IProfileWorkspaceStateService
{
    private readonly IApplicationEventBus? _eventBus;
    private RimForgeProfile? _currentProfile;
    private bool _showFullLibrary;
    public ProfileWorkspaceStateService(IApplicationEventBus? eventBus = null) => _eventBus = eventBus;

    private HashSet<string> _activePackageIds = new(StringComparer.OrdinalIgnoreCase);

    public RimForgeProfile? CurrentProfile => _currentProfile;
    public bool ShowFullLibrary => _showFullLibrary;
    public event EventHandler? WorkspaceChanged;

    public void SetCurrentProfile(RimForgeProfile? profile)
    {
        if (Equals(_currentProfile, profile)) return;

        _currentProfile = profile;
        _activePackageIds = profile?.ActiveMods
            .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        PublishChanged();
    }

    public void SetShowFullLibrary(bool showFullLibrary)
    {
        if (_showFullLibrary == showFullLibrary) return;
        _showFullLibrary = showFullLibrary;
        PublishChanged();
    }

    private void PublishChanged()
    {
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
        _eventBus?.Publish(new ProfileWorkspaceChangedEvent(_currentProfile, _showFullLibrary));
    }

    public bool Contains(ModRecord mod) =>
        !string.IsNullOrWhiteSpace(mod.PackageId) && _activePackageIds.Contains(mod.PackageId);
}
