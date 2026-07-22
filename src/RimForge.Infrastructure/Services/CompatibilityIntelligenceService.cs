using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class CompatibilityIntelligenceService : ICompatibilityIntelligenceService
{
    public IReadOnlyList<CompatibilityIntelligence> Evaluate(IEnumerable<RuntimeEvidenceRecord> evidence)
    {
        return evidence
            .Where(item => !string.IsNullOrWhiteSpace(item.SourcePackageId) || !string.IsNullOrWhiteSpace(item.TargetPackageId))
            .GroupBy(item => PairKey(item.SourcePackageId, item.TargetPackageId), StringComparer.OrdinalIgnoreCase)
            .Select(group => EvaluatePair(group.Key, group.ToArray()))
            .OrderByDescending(item => item.ConflictScore)
            .ThenByDescending(item => item.Confidence)
            .ToArray();
    }

    private static CompatibilityIntelligence EvaluatePair(string key, IReadOnlyList<RuntimeEvidenceRecord> items)
    {
        var split = key.Split('|');
        var conflictWeight = items.Sum(item => IsConflict(item) ? SeverityWeight(item.Severity) * item.Confidence * Math.Log2(item.OccurrenceCount + 1) : 0);
        var integrationWeight = items.Sum(item => IsIntegration(item) ? item.Confidence * Math.Log2(item.OccurrenceCount + 1) : 0);
        var performanceWeight = items.Where(item => item.Kind.Contains("performance", StringComparison.OrdinalIgnoreCase))
            .Sum(item => SeverityWeight(item.Severity) * item.Confidence);
        var totalWeight = Math.Max(1d, conflictWeight + integrationWeight);
        var conflict = Clamp01(conflictWeight / (totalWeight + 1d));
        var integration = Clamp01(integrationWeight / (totalWeight + 1d));
        var compatibility = Clamp01(1d - conflict + integration * 0.35d);
        var confidence = Clamp01(1d - Math.Exp(-items.Sum(item => Math.Max(0.05, item.Confidence)) / 3d));
        var repairConfidence = Clamp01(conflict * confidence * (items.Any(item => item.SourcePackageId.Length > 0 && item.TargetPackageId.Length > 0) ? 1d : 0.55d));
        var stability = conflict >= .75 ? "Critical" : conflict >= .5 ? "Unstable" : conflict >= .25 ? "Caution" : "Stable";

        return new CompatibilityIntelligence(
            split.ElementAtOrDefault(0) ?? string.Empty,
            split.ElementAtOrDefault(1) ?? string.Empty,
            compatibility,
            conflict,
            integration,
            confidence,
            Clamp01(performanceWeight / 5d),
            repairConfidence,
            stability,
            items.Sum(item => Math.Max(1, item.OccurrenceCount)),
            items.Max(item => item.LastObservedUtc),
            items.Select(item => item.Kind).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray());
    }

    private static bool IsConflict(RuntimeEvidenceRecord item) =>
        item.RelationshipKind.Contains("incompat", StringComparison.OrdinalIgnoreCase) ||
        item.Kind.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
        item.Kind.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
        item.Kind.Contains("failure", StringComparison.OrdinalIgnoreCase) ||
        item.Kind.Contains("missing-dependency", StringComparison.OrdinalIgnoreCase);

    private static bool IsIntegration(RuntimeEvidenceRecord item) =>
        item.RelationshipKind.Contains("integration", StringComparison.OrdinalIgnoreCase) ||
        item.RelationshipKind.Contains("soft-dependency", StringComparison.OrdinalIgnoreCase) ||
        item.Disposition == RuntimeEvidenceDisposition.Corroborated;

    private static double SeverityWeight(RuntimeEvidenceSeverity severity) => severity switch
    {
        RuntimeEvidenceSeverity.Critical => 2.0,
        RuntimeEvidenceSeverity.Error => 1.5,
        RuntimeEvidenceSeverity.Warning => 0.75,
        RuntimeEvidenceSeverity.Information => 0.25,
        _ => 0.1
    };

    private static string PairKey(string source, string target)
    {
        source = source?.Trim() ?? string.Empty;
        target = target?.Trim() ?? string.Empty;
        if (string.Compare(source, target, StringComparison.OrdinalIgnoreCase) <= 0) return source + "|" + target;
        return target + "|" + source;
    }

    private static double Clamp01(double value) => Math.Max(0d, Math.Min(1d, value));
}
