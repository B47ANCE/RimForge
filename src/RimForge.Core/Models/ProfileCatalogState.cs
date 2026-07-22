namespace RimForge.Core.Models;

public sealed record ProfileCatalogState(
    IReadOnlyList<string> FavoriteProfileNames,
    IReadOnlyList<string> LockedProfileNames,
    DateTimeOffset UpdatedUtc)
{
    public static ProfileCatalogState Empty { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        DateTimeOffset.MinValue);
}
