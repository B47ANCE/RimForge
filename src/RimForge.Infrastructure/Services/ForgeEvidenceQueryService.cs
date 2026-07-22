using System.Collections.ObjectModel;
using System.Diagnostics;
using RimForge.Core.Models;

namespace RimForge.Infrastructure.Services;

public interface IForgeEvidenceQueryService
{
    ForgeEvidenceQueryResult Query(ForgeEvidenceSnapshot snapshot, ForgeEvidenceQuery query);
    ForgeEvidenceDiagnostics Diagnose(ForgeEvidenceSnapshot snapshot);
}

public sealed class ForgeEvidenceQueryService : IForgeEvidenceQueryService
{
    public ForgeEvidenceQueryResult Query(ForgeEvidenceSnapshot snapshot, ForgeEvidenceQuery query)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();

        var stopwatch = Stopwatch.StartNew();
        IEnumerable<ForgeEvidenceContribution> candidates = SelectInitialCandidates(snapshot.Index, query);

        if (query.EffectiveSubjectIds.Count > 0)
        {
            var subjects = query.EffectiveSubjectIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            candidates = candidates.Where(item =>
                subjects.Contains(item.SubjectId) ||
                (query.IncludeRelatedSubjects && item.RelatedSubjectId is not null && subjects.Contains(item.RelatedSubjectId)));
        }

        if (query.EffectiveEvidenceTypes.Count > 0)
        {
            var types = query.EffectiveEvidenceTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            candidates = candidates.Where(item => types.Contains(item.EvidenceType));
        }

        if (query.EffectiveSourceKinds.Count > 0)
        {
            var sources = query.EffectiveSourceKinds.ToHashSet();
            candidates = candidates.Where(item => sources.Contains(item.Provenance.SourceKind));
        }

        if (query.MinimumConfidenceBand is not null)
            candidates = candidates.Where(item => (int)item.ConfidenceBand >= (int)query.MinimumConfidenceBand.Value);
        if (query.ObservedAfterUtc is not null)
            candidates = candidates.Where(item => item.LastObservedAtUtc >= query.ObservedAfterUtc.Value);
        if (query.ObservedBeforeUtc is not null)
            candidates = candidates.Where(item => item.FirstObservedAtUtc <= query.ObservedBeforeUtc.Value);
        if (!string.IsNullOrWhiteSpace(query.Text))
            candidates = candidates.Where(item => MatchesText(item, query.Text));

        var materialized = candidates
            .DistinctBy(item => item.EvidenceId, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(item => item.ConfidenceBand)
            .ThenByDescending(item => item.LastObservedAtUtc)
            .ThenBy(item => item.SubjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.EvidenceType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();

        var typeFacets = BuildFacets(materialized, item => item.EvidenceType);
        var sourceFacets = BuildFacets(materialized, item => item.Provenance.SourceKind.ToString());
        var page = materialized.Skip(query.Offset).Take(query.Limit).ToArray();
        stopwatch.Stop();

        return new ForgeEvidenceQueryResult(
            Array.AsReadOnly(page), materialized.Length, query.Offset, query.Limit,
            typeFacets, sourceFacets, stopwatch.Elapsed, snapshot.Generation);
    }

    public ForgeEvidenceDiagnostics Diagnose(ForgeEvidenceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var bySource = snapshot.Contributions
            .GroupBy(item => item.Provenance.SourceKind)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());
        var byConfidence = snapshot.Contributions
            .GroupBy(item => item.ConfidenceBand)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());

        return new ForgeEvidenceDiagnostics(
            snapshot.Generation,
            snapshot.SchemaVersion,
            snapshot.PlatformVersion,
            snapshot.Entries.Count,
            snapshot.Contributions.Count,
            snapshot.Index.SubjectCount,
            snapshot.Contributions.Count(item => !string.IsNullOrWhiteSpace(item.RelatedSubjectId)),
            snapshot.ProducerDiagnostics.Count,
            snapshot.Metrics.PendingInvalidations,
            snapshot.Metrics.ActiveWatchers,
            new ReadOnlyDictionary<ForgeEvidenceSourceKind, int>(bySource),
            new ReadOnlyDictionary<ForgeEvidenceConfidenceBand, int>(byConfidence),
            snapshot.PublishedAtUtc);
    }

    private static IEnumerable<ForgeEvidenceContribution> SelectInitialCandidates(ForgeEvidenceIndex index, ForgeEvidenceQuery query)
    {
        if (query.EffectiveSubjectIds.Count == 1 && string.IsNullOrWhiteSpace(query.Text))
            return index.ForSubjectOrRelated(query.EffectiveSubjectIds.Single());
        if (query.EffectiveEvidenceTypes.Count == 1 && query.EffectiveSubjectIds.Count == 0 && string.IsNullOrWhiteSpace(query.Text))
            return index.ForType(query.EffectiveEvidenceTypes.Single());
        if (query.EffectiveSourceKinds.Count == 1 && query.EffectiveSubjectIds.Count == 0 && query.EffectiveEvidenceTypes.Count == 0 && string.IsNullOrWhiteSpace(query.Text))
            return index.FromSource(query.EffectiveSourceKinds.Single());
        return index.All;
    }

    private static bool MatchesText(ForgeEvidenceContribution item, string text)
    {
        var term = text.Trim();
        if (term.Length == 0) return true;
        return Contains(item.SubjectId, term) || Contains(item.RelatedSubjectId, term) ||
               Contains(item.EvidenceType, term) || Contains(item.Summary, term) ||
               Contains(item.Provenance.SourceId, term) ||
               item.EffectiveAttributes.Any(pair => Contains(pair.Key, term) || Contains(pair.Value, term)) ||
               item.Provenance.EffectiveAttributes.Any(pair => Contains(pair.Key, term) || Contains(pair.Value, term));
    }

    private static bool Contains(string? value, string term) =>
        value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private static IReadOnlyList<ForgeEvidenceFacet> BuildFacets(
        IEnumerable<ForgeEvidenceContribution> items,
        Func<ForgeEvidenceContribution, string> selector) =>
        items.GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ForgeEvidenceFacet(group.Key, group.Count()))
            .OrderByDescending(facet => facet.Count)
            .ThenBy(facet => facet.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
