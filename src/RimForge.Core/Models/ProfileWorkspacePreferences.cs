namespace RimForge.Core.Models;

public sealed record ProfileWorkspacePreferences(
    IReadOnlyList<UserLoadOrderLock> LockedPositions,
    IReadOnlyList<string> DismissedRecommendationIds,
    IReadOnlyList<string> ExpandedExplanationPackageIds,
    DateTimeOffset UpdatedUtc)
{
    public static ProfileWorkspacePreferences Empty { get; } = new(
        Array.Empty<UserLoadOrderLock>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        DateTimeOffset.MinValue);
}
