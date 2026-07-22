using System.Collections.ObjectModel;

namespace RimForge.Core.Models;

public static class ForgeEvidenceSchema
{
    public const int CurrentVersion = 2;
    public const string PlatformVersion = "1.0";
}

public enum ForgeEvidenceSourceKind
{
    StaticAnalysis,
    DependencyAnalysis,
    RuntimeCompanion,
    HarmonyInspection,
    CommunityRule,
    UseThisInstead,
    UserOverride,
    CompatibilityIntelligence
}

public enum ForgeEvidenceConfidenceBand
{
    Unknown,
    Low,
    Medium,
    High,
    Authoritative
}

public sealed record ForgeEvidenceProvenance(
    ForgeEvidenceSourceKind SourceKind,
    string SourceId,
    string SourceVersion,
    DateTimeOffset ObservedAtUtc,
    string? SessionId = null,
    string? CorrelationId = null,
    IReadOnlyDictionary<string, string>? Attributes = null)
{
    public IReadOnlyDictionary<string, string> EffectiveAttributes { get; } =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(Attributes ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase));
}

public sealed record ForgeEvidenceContribution(
    string EvidenceId,
    string SubjectId,
    string EvidenceType,
    string Summary,
    double Confidence,
    ForgeEvidenceConfidenceBand ConfidenceBand,
    ForgeEvidenceProvenance Provenance,
    DateTimeOffset FirstObservedAtUtc,
    DateTimeOffset LastObservedAtUtc,
    int ObservationCount = 1,
    string? RelatedSubjectId = null,
    IReadOnlyDictionary<string, string>? Attributes = null)
{
    public IReadOnlyDictionary<string, string> EffectiveAttributes { get; } =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(Attributes ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase));
}

public sealed record ForgeEvidenceIngestionBatch(
    string BatchId,
    int SchemaVersion,
    ForgeEvidenceSourceKind SourceKind,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ForgeEvidenceContribution> Contributions);

public sealed record ForgeEvidenceIngestionResult(
    string BatchId,
    int Accepted,
    int Merged,
    int Rejected,
    IReadOnlyList<string> ValidationErrors);

public enum ForgeEvidenceCollectionStage
{
    Preparing,
    Collecting,
    Validating,
    Consolidating,
    Persisting,
    Completed
}

public sealed record ForgeEvidenceCollectionContext(
    IReadOnlyList<ModRecord> Mods,
    string TargetRimWorldVersion,
    ForgeEvidenceSnapshotDescriptor PreviousSnapshot,
    IReadOnlySet<string> InvalidatedRootPaths,
    bool ForceRescan,
    DateTimeOffset StartedAtUtc);

public sealed record ForgeEvidenceSnapshotDescriptor(
    int Generation,
    int SchemaVersion,
    string PlatformVersion,
    string TargetRimWorldVersion,
    DateTimeOffset PublishedAtUtc,
    int EntryCount,
    int ContributionCount);

public sealed record ForgeEvidenceProducerProgress(
    string ProducerId,
    ForgeEvidenceSourceKind SourceKind,
    ForgeEvidenceCollectionStage Stage,
    int Completed,
    int Total,
    string? Detail = null);

public sealed record ForgeEvidenceProducerDiagnostic(
    string ProducerId,
    ForgeEvidenceSourceKind SourceKind,
    string Code,
    string Message,
    bool IsTransient,
    DateTimeOffset OccurredAtUtc);

public sealed record ForgeEvidenceProducerResult(
    string ProducerId,
    ForgeEvidenceSourceKind SourceKind,
    IReadOnlyList<ForgeEvidenceContribution> Contributions,
    IReadOnlyList<ForgeEvidenceProducerDiagnostic> Diagnostics,
    TimeSpan Elapsed)
{
    public static ForgeEvidenceProducerResult Empty(
        string producerId,
        ForgeEvidenceSourceKind sourceKind,
        TimeSpan elapsed) =>
        new(producerId, sourceKind, Array.Empty<ForgeEvidenceContribution>(),
            Array.Empty<ForgeEvidenceProducerDiagnostic>(), elapsed);
}

public interface IForgeEvidenceProducer
{
    string ProducerId { get; }
    ForgeEvidenceSourceKind SourceKind { get; }
    int Order { get; }

    Task<ForgeEvidenceProducerResult> CollectAsync(
        ForgeEvidenceCollectionContext context,
        IProgress<ForgeEvidenceProducerProgress>? progress,
        CancellationToken cancellationToken);
}
