using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class ProfileEditService(IProfileWorkspaceService profileWorkspaceService) : IProfileEditService
{
    private static readonly StringComparer PackageComparer = StringComparer.OrdinalIgnoreCase;

    public ProfileEditDraft CreateDraft(
        LibraryProfileWorkspaceSnapshot workspace,
        string profileName,
        IReadOnlyList<string> proposedActiveMods)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(proposedActiveMods);
        var projection = workspace.FindProfile(profileName)
            ?? throw new KeyNotFoundException($"Profile '{profileName}' was not found in the current workspace.");
        var proposed = proposedActiveMods.Select(Normalize).Where(id => id.Length > 0).ToArray();
        var errors = Validate(projection.Profile, proposed, workspace).ToArray();
        return new ProfileEditDraft(
            projection.Profile,
            workspace.Fingerprint,
            proposed,
            Compare(projection.Profile.ActiveMods, proposed),
            errors);
    }

    public async Task<ProfileEditCommitResult> CommitAsync(
        ProfileEditDraft draft,
        LibraryProfileWorkspaceSnapshot currentWorkspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(currentWorkspace);
        if (!string.Equals(draft.BaseWorkspaceFingerprint, currentWorkspace.Fingerprint, StringComparison.Ordinal))
            return new(false, "The library or profile changed after editing began. Refresh and review the draft again.", IsStale: true);
        if (!draft.CanCommit)
            return new(false, draft.ValidationErrors.FirstOrDefault() ?? "The profile draft has no committable changes.");

        var current = currentWorkspace.FindProfile(draft.Profile.Name);
        if (current is null)
            return new(false, $"Profile '{draft.Profile.Name}' no longer exists.", IsStale: true);
        var result = await profileWorkspaceService.SaveLoadOrderAsync(current.Profile, draft.ProposedActiveMods, cancellationToken);
        return new(result.Success, result.Message, result.UpdatedProfile, result.BackupPath);
    }

    private static IEnumerable<string> Validate(
        RimForgeProfile profile,
        IReadOnlyList<string> proposed,
        LibraryProfileWorkspaceSnapshot workspace)
    {
        if (profile.IsLocked) yield return $"Profile '{profile.Name}' is locked.";
        if (proposed.Count == 0) yield return "A profile load order cannot be empty.";
        if (!proposed.Contains("ludeon.rimworld", PackageComparer)) yield return "Core must remain enabled in every profile.";
        foreach (var duplicate in proposed.GroupBy(id => id, PackageComparer).Where(group => group.Count() > 1).Select(group => group.Key))
            yield return $"Package '{duplicate}' appears more than once in the proposed load order.";
        var installed = workspace.InstalledMods.Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
            .Select(mod => Normalize(mod.PackageId)).ToHashSet(PackageComparer);
        foreach (var missing in proposed.Where(id => !installed.Contains(id)).Distinct(PackageComparer))
            yield return $"Package '{missing}' is not installed.";
        foreach (var ambiguous in proposed.Where(id => workspace.DuplicatePackageIds.Contains(id, PackageComparer)).Distinct(PackageComparer))
            yield return $"Package '{ambiguous}' resolves to multiple installed mods.";
    }

    private static ProfileEditChangeSet Compare(IReadOnlyList<string> current, IReadOnlyList<string> proposed)
    {
        var currentIds = current.Select(Normalize).ToArray();
        var currentSet = currentIds.ToHashSet(PackageComparer);
        var proposedSet = proposed.ToHashSet(PackageComparer);
        var added = proposed.Where(id => !currentSet.Contains(id)).Distinct(PackageComparer).ToArray();
        var removed = currentIds.Where(id => !proposedSet.Contains(id)).Distinct(PackageComparer).ToArray();
        var currentPositions = currentIds.Select((id, index) => (id, index)).GroupBy(item => item.id, PackageComparer)
            .ToDictionary(group => group.Key, group => group.First().index, PackageComparer);
        var moves = proposed.Select((id, index) => (id, index))
            .Where(item => currentPositions.TryGetValue(item.id, out var oldIndex) && oldIndex != item.index)
            .Select(item => new ProfileOrderChange(item.id, currentPositions[item.id], item.index)).ToArray();
        return new ProfileEditChangeSet(added, removed, moves);
    }

    private static string Normalize(string? packageId) => packageId?.Trim().ToLowerInvariant() ?? string.Empty;
}
