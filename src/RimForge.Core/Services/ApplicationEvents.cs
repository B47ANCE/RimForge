using RimForge.Core.BackgroundTasks;
using RimForge.Core.Models;

namespace RimForge.Core.Services;

public sealed record SearchQueryChangedEvent(string QueryText);
public sealed record ModSelectionChangedEvent(ModRecord? SelectedMod);
public sealed record ProfileWorkspaceChangedEvent(RimForgeProfile? CurrentProfile, bool ShowFullLibrary);
public sealed record NavigationChangedEvent(string? CurrentPackageId, bool CanGoBack, bool CanGoForward);
public sealed record ForgeSessionChangedEvent(ForgeSessionSnapshot Snapshot);
public sealed record BackgroundTaskChangedEvent(BackgroundTaskSnapshot Snapshot);

// Stable extension events for feature passes that have not yet moved their state
// into shared services. New publishers can adopt these without changing the bus.
public sealed record ModLibraryChangedEvent(int ModCount, string Reason);
public sealed record IssueStateChangedEvent(int IssueCount, string Reason);
public sealed record SettingsChangedEvent(IReadOnlyCollection<string> ChangedKeys);
