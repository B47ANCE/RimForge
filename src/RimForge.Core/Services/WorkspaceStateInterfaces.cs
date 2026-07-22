using RimForge.Core.Models;

namespace RimForge.Core.Services;

public interface ISelectionService
{
    ModRecord? SelectedMod { get; }
    event EventHandler<ModRecord?>? SelectionChanged;
    void Select(ModRecord? mod);
}

public interface IProfileWorkspaceStateService
{
    RimForgeProfile? CurrentProfile { get; }
    bool ShowFullLibrary { get; }
    event EventHandler? WorkspaceChanged;
    void SetCurrentProfile(RimForgeProfile? profile);
    void SetShowFullLibrary(bool showFullLibrary);
    bool Contains(ModRecord mod);
}

public interface IModFilteringService
{
    bool Matches(ModRecord mod, ModFilterCriteria criteria);
}
