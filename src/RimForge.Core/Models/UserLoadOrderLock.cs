namespace RimForge.Core.Models;

public sealed record UserLoadOrderLock(
    string PackageId,
    int Position,
    DateTimeOffset CreatedUtc,
    string? Note = null)
{
    public int NormalizedPosition(int count) => Math.Clamp(Position, 0, Math.Max(0, count - 1));
}

public sealed record LoadOrderLockConflict(
    string PackageId,
    int RequestedPosition,
    string BlockingPackageId,
    string Explanation,
    bool IsRecoverable,
    IReadOnlyList<int> SuggestedPositions);
