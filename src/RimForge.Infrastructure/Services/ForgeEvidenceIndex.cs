using System.Collections.ObjectModel;
using RimForge.Core.Models;

namespace RimForge.Infrastructure.Services;

public sealed class ForgeEvidenceIndex
{
    public static ForgeEvidenceIndex Empty { get; } = new(Array.Empty<ForgeEvidenceContribution>());

    private readonly IReadOnlyDictionary<string, IReadOnlyList<ForgeEvidenceContribution>> _bySubject;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ForgeEvidenceContribution>> _byType;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ForgeEvidenceContribution>> _byRelatedSubject;
    private readonly IReadOnlyDictionary<ForgeEvidenceSourceKind, IReadOnlyList<ForgeEvidenceContribution>> _bySource;

    public ForgeEvidenceIndex(IEnumerable<ForgeEvidenceContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        var stable = contributions
            .OrderBy(item => item.SubjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.EvidenceType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RelatedSubjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Provenance.SourceKind)
            .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();

        All = Array.AsReadOnly(stable);
        _bySubject = BuildStringIndex(stable, item => item.SubjectId);
        _byType = BuildStringIndex(stable, item => item.EvidenceType);
        _byRelatedSubject = BuildStringIndex(stable.Where(item => !string.IsNullOrWhiteSpace(item.RelatedSubjectId)), item => item.RelatedSubjectId!);
        _bySource = new ReadOnlyDictionary<ForgeEvidenceSourceKind, IReadOnlyList<ForgeEvidenceContribution>>(
            stable.GroupBy(item => item.Provenance.SourceKind)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ForgeEvidenceContribution>)Array.AsReadOnly(group.ToArray())));
    }

    public IReadOnlyList<ForgeEvidenceContribution> All { get; }
    public int SubjectCount => _bySubject.Count;
    public int EvidenceTypeCount => _byType.Count;


    public IReadOnlyList<ForgeEvidenceContribution> ForSubject(string? subjectId) =>
        Lookup(_bySubject, subjectId);

    public IReadOnlyList<ForgeEvidenceContribution> ForType(string? evidenceType) =>
        Lookup(_byType, evidenceType);

    public IReadOnlyList<ForgeEvidenceContribution> ForRelatedSubject(string? subjectId) =>
        Lookup(_byRelatedSubject, subjectId);

    public IReadOnlyList<ForgeEvidenceContribution> ForSubjectOrRelated(string? subjectId) =>
        ForSubject(subjectId)
            .Concat(ForRelatedSubject(subjectId))
            .DistinctBy(item => item.EvidenceId, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(item => item.ConfidenceBand)
            .ThenBy(item => item.EvidenceType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<ForgeEvidenceContribution> FromSource(ForgeEvidenceSourceKind sourceKind) =>
        _bySource.TryGetValue(sourceKind, out var values)
            ? values
            : Array.Empty<ForgeEvidenceContribution>();

    public IReadOnlyList<ForgeEvidenceContribution> Between(string? firstSubjectId, string? secondSubjectId)
    {
        if (string.IsNullOrWhiteSpace(firstSubjectId) || string.IsNullOrWhiteSpace(secondSubjectId))
            return Array.Empty<ForgeEvidenceContribution>();

        return ForSubject(firstSubjectId)
            .Where(item => string.Equals(item.RelatedSubjectId, secondSubjectId, StringComparison.OrdinalIgnoreCase))
            .Concat(ForSubject(secondSubjectId)
                .Where(item => string.Equals(item.RelatedSubjectId, firstSubjectId, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(item => item.EvidenceId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.EvidenceType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Provenance.SourceKind)
            .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ForgeEvidenceContribution>> BuildStringIndex(
        IEnumerable<ForgeEvidenceContribution> values,
        Func<ForgeEvidenceContribution, string> keySelector) =>
        new ReadOnlyDictionary<string, IReadOnlyList<ForgeEvidenceContribution>>(
            values.GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ForgeEvidenceContribution>)Array.AsReadOnly(group.ToArray()),
                    StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<ForgeEvidenceContribution> Lookup(
        IReadOnlyDictionary<string, IReadOnlyList<ForgeEvidenceContribution>> index,
        string? key) =>
        !string.IsNullOrWhiteSpace(key) && index.TryGetValue(key, out var values)
            ? values
            : Array.Empty<ForgeEvidenceContribution>();
}
