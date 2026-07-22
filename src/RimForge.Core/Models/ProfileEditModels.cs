namespace RimForge.Core.Models;

public sealed record ProfileEditChangeSet(
    IReadOnlyList<string> AddedPackageIds,
    IReadOnlyList<string> RemovedPackageIds,
    IReadOnlyList<ProfileOrderChange> OrderChanges)
{
    public bool HasChanges => AddedPackageIds.Count > 0 || RemovedPackageIds.Count > 0 || OrderChanges.Count > 0;
}

public sealed record ProfileEditDraft(
    RimForgeProfile Profile,
    string BaseWorkspaceFingerprint,
    IReadOnlyList<string> ProposedActiveMods,
    ProfileEditChangeSet Changes,
    IReadOnlyList<string> ValidationErrors)
{
    public bool CanCommit => !Profile.IsLocked && Changes.HasChanges && ValidationErrors.Count == 0;
}

public sealed record ProfileEditCommitResult(
    bool Success,
    string Message,
    RimForgeProfile? UpdatedProfile = null,
    string? BackupPath = null,
    bool IsStale = false);
