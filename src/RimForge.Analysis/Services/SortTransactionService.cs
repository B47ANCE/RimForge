using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Analysis.Services;

public sealed record SortTransactionPreview(
    Guid TransactionId,
    IReadOnlyList<string> OriginalOrder,
    IReadOnlyList<string> ProposedOrder,
    IReadOnlyList<LoadOrderLockConflict> Conflicts,
    DateTimeOffset CreatedUtc)
{
    public bool CanApply => Conflicts.Count == 0 && ProposedOrder.Count > 0;
}

public sealed record SortTransactionResult(
    bool Success,
    string Message,
    RimForgeProfile? UpdatedProfile = null,
    IReadOnlyList<string>? RestoredOrder = null);

public sealed class SortTransactionService
{
    private readonly IProfileWorkspaceService _profiles;

    public SortTransactionService(IProfileWorkspaceService profiles) => _profiles = profiles;

    public SortTransactionPreview CreatePreview(
        IReadOnlyList<string> originalOrder,
        IReadOnlyList<string> proposedOrder,
        IReadOnlyList<LoadOrderLockConflict>? conflicts = null) =>
        new(Guid.NewGuid(), originalOrder.ToArray(), proposedOrder.ToArray(),
            conflicts?.ToArray() ?? Array.Empty<LoadOrderLockConflict>(), DateTimeOffset.UtcNow);

    public async Task<SortTransactionResult> ApplyAsync(
        RimForgeProfile profile,
        SortTransactionPreview preview,
        CancellationToken cancellationToken = default)
    {
        if (!preview.CanApply)
            return new(false, "The sort preview contains conflicts and was not applied.", RestoredOrder: preview.OriginalOrder);

        var saved = await _profiles.SaveLoadOrderAsync(profile, preview.ProposedOrder, cancellationToken);
        if (saved.Success)
            return new(true, saved.Message, saved.UpdatedProfile);

        return new(false,
            $"The proposed order could not be saved. The profile remains unchanged. {saved.Message}",
            RestoredOrder: preview.OriginalOrder);
    }
}
