using System.Collections.ObjectModel;

namespace RimForge.Core.Models;

public sealed record ForgeEvidenceQuery(
    string? Text = null,
    IReadOnlyCollection<string>? SubjectIds = null,
    IReadOnlyCollection<string>? EvidenceTypes = null,
    IReadOnlyCollection<ForgeEvidenceSourceKind>? SourceKinds = null,
    ForgeEvidenceConfidenceBand? MinimumConfidenceBand = null,
    DateTimeOffset? ObservedAfterUtc = null,
    DateTimeOffset? ObservedBeforeUtc = null,
    bool IncludeRelatedSubjects = true,
    int Offset = 0,
    int Limit = 250)
{
    public IReadOnlyCollection<string> EffectiveSubjectIds { get; } =
        new ReadOnlyCollection<string>((SubjectIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    public IReadOnlyCollection<string> EffectiveEvidenceTypes { get; } =
        new ReadOnlyCollection<string>((EvidenceTypes ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    public IReadOnlyCollection<ForgeEvidenceSourceKind> EffectiveSourceKinds { get; } =
        new ReadOnlyCollection<ForgeEvidenceSourceKind>((SourceKinds ?? Array.Empty<ForgeEvidenceSourceKind>())
            .Distinct()
            .OrderBy(value => value)
            .ToArray());

    public void Validate()
    {
        if (Offset < 0) throw new ArgumentOutOfRangeException(nameof(Offset));
        if (Limit is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(Limit));
        if (ObservedAfterUtc is not null && ObservedBeforeUtc is not null && ObservedAfterUtc.Value > ObservedBeforeUtc.Value)
            throw new ArgumentException("ObservedAfterUtc must not be later than ObservedBeforeUtc.");
    }
}

public sealed record ForgeEvidenceFacet(string Value, int Count);

public sealed record ForgeEvidenceQueryResult(
    IReadOnlyList<ForgeEvidenceContribution> Items,
    int TotalMatches,
    int Offset,
    int Limit,
    IReadOnlyList<ForgeEvidenceFacet> EvidenceTypeFacets,
    IReadOnlyList<ForgeEvidenceFacet> SourceFacets,
    TimeSpan Elapsed,
    int EvidenceGeneration);

public sealed record ForgeEvidenceDiagnostics(
    int Generation,
    int SchemaVersion,
    string PlatformVersion,
    int EntryCount,
    int ContributionCount,
    int SubjectCount,
    int RelationshipCount,
    int DiagnosticCount,
    int PendingInvalidations,
    int ActiveWatchers,
    IReadOnlyDictionary<ForgeEvidenceSourceKind, int> ContributionsBySource,
    IReadOnlyDictionary<ForgeEvidenceConfidenceBand, int> ContributionsByConfidence,
    DateTimeOffset PublishedAtUtc)
{
    public bool IsHealthy => DiagnosticCount == 0 && PendingInvalidations == 0;
}
