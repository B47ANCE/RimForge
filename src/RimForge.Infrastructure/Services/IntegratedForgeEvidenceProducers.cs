using System.Diagnostics;
using System.Globalization;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public static class ForgeEvidenceProducerFactory
{
    public static IReadOnlyList<IForgeEvidenceProducer> Create(IRuntimeEvidenceStore? runtimeStore = null) =>
    [
        new StaticModMetadataEvidenceProducer(),
        new DependencyMetadataEvidenceProducer(),
        new HarmonyMetadataEvidenceProducer(),
        new CommunityRuleEvidenceProducer(LoadOrderPolicyPack.LoadDefault()),
        new UseThisInsteadEvidenceProducer(UseThisInsteadDatabase.LoadDefault()),
        .. runtimeStore is null
            ? Array.Empty<IForgeEvidenceProducer>()
            : new IForgeEvidenceProducer[]
            {
                new RuntimeCompanionEvidenceProducer(runtimeStore),
                new CompatibilityIntelligenceEvidenceProducer(runtimeStore)
            }
    ];
}

public sealed class HarmonyMetadataEvidenceProducer : IForgeEvidenceProducer
{
    public string ProducerId => "rimforge.harmony-metadata";
    public ForgeEvidenceSourceKind SourceKind => ForgeEvidenceSourceKind.HarmonyInspection;
    public int Order => 300;

    public Task<ForgeEvidenceProducerResult> CollectAsync(ForgeEvidenceCollectionContext context, IProgress<ForgeEvidenceProducerProgress>? progress, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<ForgeEvidenceContribution>();
        var mods = context.Mods.OrderBy(m => m.PackageId ?? m.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        for (var i = 0; i < mods.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mod = mods[i];
            if (mod.Evidence.HarmonyHintCount > 0)
            {
                var subject = Subject(mod);
                results.Add(Create(subject, "harmony-presence", $"{mod.DisplayName} contains {mod.Evidence.HarmonyHintCount:N0} Harmony ownership hint(s).", .85,
                    mod.LastModified == default ? context.StartedAtUtc : mod.LastModified,
                    new Dictionary<string, string> { ["hintCount"] = mod.Evidence.HarmonyHintCount.ToString(CultureInfo.InvariantCulture), ["rootPath"] = mod.RootPath }));
            }
            progress?.Report(new(ProducerId, SourceKind, ForgeEvidenceCollectionStage.Collecting, i + 1, mods.Length, mod.DisplayName));
        }
        return Task.FromResult(new ForgeEvidenceProducerResult(ProducerId, SourceKind, results, [], sw.Elapsed));
    }

    private ForgeEvidenceContribution Create(string subject, string type, string summary, double confidence, DateTimeOffset observed, IReadOnlyDictionary<string,string> attributes) =>
        new(string.Empty, subject, type, summary, confidence, ForgeEvidenceConfidenceBand.High,
            new(SourceKind, ProducerId, ForgeEvidenceSchema.PlatformVersion, observed), observed, observed, 1, null, attributes);
    private static string Subject(ModRecord mod) => string.IsNullOrWhiteSpace(mod.PackageId) ? mod.Id.Trim() : mod.PackageId.Trim().ToLowerInvariant();
}

public sealed class CommunityRuleEvidenceProducer(LoadOrderPolicyPack policy) : IForgeEvidenceProducer
{
    public string ProducerId => "rimforge.community-rules";
    public ForgeEvidenceSourceKind SourceKind => ForgeEvidenceSourceKind.CommunityRule;
    public int Order => 400;

    public Task<ForgeEvidenceProducerResult> CollectAsync(ForgeEvidenceCollectionContext context, IProgress<ForgeEvidenceProducerProgress>? progress, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var installed = context.Mods.Select(Subject).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<ForgeEvidenceContribution>();
        foreach (var rule in policy.GetApplicablePackageRules(context.TargetRimWorldVersion).OrderBy(r => r.RuleId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(rule.PackageId) || !installed.Contains(rule.PackageId)) continue;
            results.Add(Create(rule.PackageId, "community-category", rule.Reason, Confidence(rule.Confidence), null, rule.RuleId,
                new Dictionary<string,string> { ["category"] = rule.Category.ToString(), ["source"] = rule.Source }));
        }
        foreach (var rule in policy.GetApplicableRelativeRules(context.TargetRimWorldVersion).OrderBy(r => r.RuleId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!installed.Contains(rule.BeforePackageId) || !installed.Contains(rule.AfterPackageId)) continue;
            results.Add(Create(rule.BeforePackageId, "community-load-before", rule.Reason, Confidence(rule.Confidence), rule.AfterPackageId, rule.RuleId,
                new Dictionary<string,string> { ["source"] = rule.Source }));
        }
        return Task.FromResult(new ForgeEvidenceProducerResult(ProducerId, SourceKind, results, policy.Diagnostics.Select(d =>
            new ForgeEvidenceProducerDiagnostic(ProducerId, SourceKind, d.Code, d.Message, false, context.StartedAtUtc)).ToArray(), sw.Elapsed));
    }

    private ForgeEvidenceContribution Create(string subject, string type, string summary, double confidence, string? related, string ruleId, IReadOnlyDictionary<string,string> attrs) =>
        new(string.Empty, subject.ToLowerInvariant(), type, summary, confidence, Band(confidence),
            new(SourceKind, ProducerId, policy.ContentVersion, DateTimeOffset.UtcNow, CorrelationId: ruleId), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, related?.ToLowerInvariant(), attrs);
    private static double Confidence(LoadOrderRuleConfidence value) => value switch { LoadOrderRuleConfidence.Hard => 1, LoadOrderRuleConfidence.Recommended => .85, _ => .65 };
    private static ForgeEvidenceConfidenceBand Band(double value) => value >= .98 ? ForgeEvidenceConfidenceBand.Authoritative : value >= .8 ? ForgeEvidenceConfidenceBand.High : ForgeEvidenceConfidenceBand.Medium;
    private static string Subject(ModRecord mod) => string.IsNullOrWhiteSpace(mod.PackageId) ? mod.Id : mod.PackageId;
}

public sealed class UseThisInsteadEvidenceProducer(UseThisInsteadDatabase database) : IForgeEvidenceProducer
{
    public string ProducerId => "rimforge.use-this-instead";
    public ForgeEvidenceSourceKind SourceKind => ForgeEvidenceSourceKind.UseThisInstead;
    public int Order => 500;
    public Task<ForgeEvidenceProducerResult> CollectAsync(ForgeEvidenceCollectionContext context, IProgress<ForgeEvidenceProducerProgress>? progress, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<ForgeEvidenceContribution>();
        foreach (var mod in context.Mods.OrderBy(Subject, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subject = Subject(mod);
            foreach (var rule in database.GetApplicable(subject, context.TargetRimWorldVersion).OrderBy(r => r.RuleId, StringComparer.Ordinal))
            {
                var confidence = rule.Confidence == LoadOrderRuleConfidence.Hard ? 1d : .9d;
                results.Add(new(string.Empty, subject.ToLowerInvariant(), "replacement-recommendation", rule.Reason, confidence,
                    confidence == 1 ? ForgeEvidenceConfidenceBand.Authoritative : ForgeEvidenceConfidenceBand.High,
                    new(SourceKind, ProducerId, database.ContentVersion, context.StartedAtUtc, CorrelationId: rule.RuleId),
                    context.StartedAtUtc, context.StartedAtUtc, 1, rule.ReplacementPackageId.ToLowerInvariant(),
                    new Dictionary<string,string> { ["source"] = rule.Source, ["replacementPackageId"] = rule.ReplacementPackageId }));
            }
        }
        return Task.FromResult(new ForgeEvidenceProducerResult(ProducerId, SourceKind, results, [], sw.Elapsed));
    }
    private static string Subject(ModRecord mod) => string.IsNullOrWhiteSpace(mod.PackageId) ? mod.Id : mod.PackageId;
}

public sealed class RuntimeCompanionEvidenceProducer(IRuntimeEvidenceStore store) : IForgeEvidenceProducer
{
    public string ProducerId => "rimforge.runtime-companion";
    public ForgeEvidenceSourceKind SourceKind => ForgeEvidenceSourceKind.RuntimeCompanion;
    public int Order => 600;
    public Task<ForgeEvidenceProducerResult> CollectAsync(ForgeEvidenceCollectionContext context, IProgress<ForgeEvidenceProducerProgress>? progress, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var evidence = store.Current.Evidence.OrderBy(e => e.EvidenceId, StringComparer.Ordinal).Select(item =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subject = string.IsNullOrWhiteSpace(item.SourcePackageId) ? item.TargetPackageId : item.SourcePackageId;
            return new ForgeEvidenceContribution(item.EvidenceId, subject.ToLowerInvariant(), "runtime:" + item.Kind, item.Summary, Math.Clamp(item.Confidence, 0, 1), Band(item.Confidence),
                new(SourceKind, ProducerId, ForgeEvidenceSchema.PlatformVersion, item.LastObservedUtc, item.SessionId, item.Fingerprint,
                    new Dictionary<string,string> { ["provenance"] = item.Provenance, ["severity"] = item.Severity.ToString(), ["disposition"] = item.Disposition.ToString() }),
                item.FirstObservedUtc, item.LastObservedUtc, Math.Max(1, item.OccurrenceCount), string.IsNullOrWhiteSpace(item.TargetPackageId) ? null : item.TargetPackageId.ToLowerInvariant(), item.Attributes);
        }).ToArray();
        return Task.FromResult(new ForgeEvidenceProducerResult(ProducerId, SourceKind, evidence, [], sw.Elapsed));
    }
    private static ForgeEvidenceConfidenceBand Band(double value) => value >= .95 ? ForgeEvidenceConfidenceBand.Authoritative : value >= .75 ? ForgeEvidenceConfidenceBand.High : value >= .45 ? ForgeEvidenceConfidenceBand.Medium : ForgeEvidenceConfidenceBand.Low;
}

public sealed class CompatibilityIntelligenceEvidenceProducer(IRuntimeEvidenceStore store) : IForgeEvidenceProducer
{
    public string ProducerId => "rimforge.compatibility-intelligence";
    public ForgeEvidenceSourceKind SourceKind => ForgeEvidenceSourceKind.CompatibilityIntelligence;
    public int Order => 700;
    public Task<ForgeEvidenceProducerResult> CollectAsync(ForgeEvidenceCollectionContext context, IProgress<ForgeEvidenceProducerProgress>? progress, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var results = store.Current.Compatibility.OrderBy(c => c.SourcePackageId, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.TargetPackageId, StringComparer.OrdinalIgnoreCase).Select(item =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var summary = $"Runtime compatibility between {item.SourcePackageId} and {item.TargetPackageId} is rated {item.StabilityRating}.";
            return new ForgeEvidenceContribution(string.Empty, item.SourcePackageId.ToLowerInvariant(), "compatibility-assessment", summary, item.Confidence,
                item.Confidence >= .75 ? ForgeEvidenceConfidenceBand.High : ForgeEvidenceConfidenceBand.Medium,
                new(SourceKind, ProducerId, ForgeEvidenceSchema.PlatformVersion, item.LastObservedUtc), item.LastObservedUtc, item.LastObservedUtc,
                Math.Max(1, item.ObservationCount), item.TargetPackageId.ToLowerInvariant(), new Dictionary<string,string>
                { ["compatibilityScore"] = item.CompatibilityScore.ToString("R", CultureInfo.InvariantCulture), ["conflictScore"] = item.ConflictScore.ToString("R", CultureInfo.InvariantCulture), ["stability"] = item.StabilityRating });
        }).ToArray();
        return Task.FromResult(new ForgeEvidenceProducerResult(ProducerId, SourceKind, results, [], sw.Elapsed));
    }
}
