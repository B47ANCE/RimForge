using System.Diagnostics;
using RimForge.Core.Models;

namespace RimForge.Infrastructure.Services;

public sealed class StaticModMetadataEvidenceProducer : IForgeEvidenceProducer
{
    public string ProducerId => "rimforge.static-mod-metadata";
    public ForgeEvidenceSourceKind SourceKind => ForgeEvidenceSourceKind.StaticAnalysis;
    public int Order => 100;

    public Task<ForgeEvidenceProducerResult> CollectAsync(
        ForgeEvidenceCollectionContext context,
        IProgress<ForgeEvidenceProducerProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var stopwatch = Stopwatch.StartNew();
        var contributions = new List<ForgeEvidenceContribution>();
        var orderedMods = context.Mods
            .OrderBy(mod => mod.PackageId ?? mod.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var index = 0; index < orderedMods.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mod = orderedMods[index];
            var subjectId = NormalizeSubjectId(mod);
            var observedAt = mod.LastModified == default ? context.StartedAtUtc : mod.LastModified;
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rootPath"] = mod.RootPath,
                ["source"] = mod.Source.ToString(),
                ["targetRimWorldVersion"] = context.TargetRimWorldVersion,
                ["totalFiles"] = mod.Evidence.TotalFiles.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["totalBytes"] = mod.Evidence.TotalBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            contributions.Add(Create(
                subjectId,
                "mod-inventory",
                $"{mod.DisplayName} contains {mod.Evidence.TotalFiles:N0} file(s) totaling {mod.Evidence.TotalBytes:N0} bytes.",
                1,
                observedAt,
                attributes));

            foreach (var badge in mod.Evidence.Badges
                         .OrderBy(item => item.Kind)
                         .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase))
            {
                contributions.Add(Create(
                    subjectId,
                    $"capability:{badge.Kind}",
                    string.IsNullOrWhiteSpace(badge.Summary)
                        ? $"{mod.DisplayName} contains {badge.Label} evidence."
                        : badge.Summary,
                    0.95,
                    observedAt,
                    attributes
                        .Concat(new[]
                        {
                            new KeyValuePair<string, string>("badge", badge.Label),
                            new KeyValuePair<string, string>("count", badge.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                            new KeyValuePair<string, string>("details", badge.Details)
                        })
                        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)));
            }

            foreach (var error in mod.Errors.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                contributions.Add(Create(
                    subjectId,
                    "metadata-error",
                    error,
                    1,
                    observedAt,
                    attributes));
            }

            progress?.Report(new ForgeEvidenceProducerProgress(
                ProducerId,
                SourceKind,
                ForgeEvidenceCollectionStage.Collecting,
                index + 1,
                orderedMods.Length,
                mod.DisplayName));
        }

        stopwatch.Stop();
        return Task.FromResult(new ForgeEvidenceProducerResult(
            ProducerId,
            SourceKind,
            contributions,
            Array.Empty<ForgeEvidenceProducerDiagnostic>(),
            stopwatch.Elapsed));
    }

    private ForgeEvidenceContribution Create(
        string subjectId,
        string evidenceType,
        string summary,
        double confidence,
        DateTimeOffset observedAt,
        IReadOnlyDictionary<string, string> attributes) =>
        new(
            string.Empty,
            subjectId,
            evidenceType,
            summary,
            confidence,
            confidence >= 0.98 ? ForgeEvidenceConfidenceBand.Authoritative : ForgeEvidenceConfidenceBand.High,
            new ForgeEvidenceProvenance(
                SourceKind,
                ProducerId,
                ForgeEvidenceSchema.PlatformVersion,
                observedAt),
            observedAt,
            observedAt,
            1,
            null,
            attributes);

    private static string NormalizeSubjectId(ModRecord mod) =>
        string.IsNullOrWhiteSpace(mod.PackageId) ? mod.Id.Trim() : mod.PackageId.Trim().ToLowerInvariant();
}

public sealed class DependencyMetadataEvidenceProducer : IForgeEvidenceProducer
{
    public string ProducerId => "rimforge.dependency-metadata";
    public ForgeEvidenceSourceKind SourceKind => ForgeEvidenceSourceKind.DependencyAnalysis;
    public int Order => 200;

    public Task<ForgeEvidenceProducerResult> CollectAsync(
        ForgeEvidenceCollectionContext context,
        IProgress<ForgeEvidenceProducerProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var stopwatch = Stopwatch.StartNew();
        var contributions = new List<ForgeEvidenceContribution>();
        var orderedMods = context.Mods
            .OrderBy(mod => mod.PackageId ?? mod.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var index = 0; index < orderedMods.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mod = orderedMods[index];
            var subjectId = NormalizeSubjectId(mod);
            var observedAt = mod.LastModified == default ? context.StartedAtUtc : mod.LastModified;

            foreach (var dependency in mod.Dependencies
                         .OrderBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(dependency.PackageId)) continue;
                contributions.Add(CreateRelationship(
                    subjectId,
                    dependency.PackageId,
                    "required-dependency",
                    $"{mod.DisplayName} requires {dependency.DisplayName ?? dependency.PackageId}.",
                    observedAt));
            }

            foreach (var packageId in mod.LoadAfter.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(packageId)) continue;
                contributions.Add(CreateRelationship(
                    subjectId,
                    packageId,
                    "load-after",
                    $"{mod.DisplayName} declares that it loads after {packageId}.",
                    observedAt));
            }

            foreach (var packageId in mod.LoadBefore.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(packageId)) continue;
                contributions.Add(CreateRelationship(
                    subjectId,
                    packageId,
                    "load-before",
                    $"{mod.DisplayName} declares that it loads before {packageId}.",
                    observedAt));
            }

            foreach (var packageId in mod.IncompatibleWith.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(packageId)) continue;
                contributions.Add(CreateRelationship(
                    subjectId,
                    packageId,
                    "declared-incompatibility",
                    $"{mod.DisplayName} declares an incompatibility with {packageId}.",
                    observedAt));
            }

            progress?.Report(new ForgeEvidenceProducerProgress(
                ProducerId,
                SourceKind,
                ForgeEvidenceCollectionStage.Collecting,
                index + 1,
                orderedMods.Length,
                mod.DisplayName));
        }

        stopwatch.Stop();
        return Task.FromResult(new ForgeEvidenceProducerResult(
            ProducerId,
            SourceKind,
            contributions,
            Array.Empty<ForgeEvidenceProducerDiagnostic>(),
            stopwatch.Elapsed));
    }

    private ForgeEvidenceContribution CreateRelationship(
        string subjectId,
        string relatedSubjectId,
        string evidenceType,
        string summary,
        DateTimeOffset observedAt) =>
        new(
            string.Empty,
            subjectId,
            evidenceType,
            summary,
            1,
            ForgeEvidenceConfidenceBand.Authoritative,
            new ForgeEvidenceProvenance(
                SourceKind,
                ProducerId,
                ForgeEvidenceSchema.PlatformVersion,
                observedAt),
            observedAt,
            observedAt,
            1,
            relatedSubjectId.Trim().ToLowerInvariant());

    private static string NormalizeSubjectId(ModRecord mod) =>
        string.IsNullOrWhiteSpace(mod.PackageId) ? mod.Id.Trim() : mod.PackageId.Trim().ToLowerInvariant();
}
