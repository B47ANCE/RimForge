namespace RimForge.Core.Models;

public enum RuntimeEvidenceSeverity { Trace, Information, Warning, Error, Critical }
public enum RuntimeEvidenceDisposition { Observed, Corroborated, Disputed, Superseded }

public sealed record RuntimeEvidenceRecord(
    string EvidenceId,
    string SessionId,
    string Fingerprint,
    string Kind,
    string RelationshipKind,
    RuntimeEvidenceSeverity Severity,
    double Confidence,
    string SourcePackageId,
    string TargetPackageId,
    string SourceAssembly,
    string TargetAssembly,
    string Title,
    string Summary,
    string Detail,
    string ExceptionType,
    string StackTrace,
    string Provenance,
    DateTimeOffset FirstObservedUtc,
    DateTimeOffset LastObservedUtc,
    int OccurrenceCount,
    RuntimeEvidenceDisposition Disposition,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record RuntimeEvidenceSession(
    string SessionId,
    DateTimeOffset StartedUtc,
    DateTimeOffset? EndedUtc,
    string AgentVersion,
    string GameVersion,
    string ProfileName,
    string EnvironmentFingerprint,
    IReadOnlyList<string> ActivePackageIds,
    IReadOnlyList<string> ActiveDlcPackageIds,
    string EndReason = "");

public sealed record CompatibilityIntelligence(
    string SourcePackageId,
    string TargetPackageId,
    double CompatibilityScore,
    double ConflictScore,
    double IntegrationScore,
    double Confidence,
    double PerformanceImpact,
    double RepairConfidence,
    string StabilityRating,
    int ObservationCount,
    DateTimeOffset LastObservedUtc,
    IReadOnlyList<string> EvidenceKinds);

public sealed record RuntimeEvidenceSnapshot(
    IReadOnlyList<RuntimeEvidenceSession> Sessions,
    IReadOnlyList<RuntimeEvidenceRecord> Evidence,
    IReadOnlyList<CompatibilityIntelligence> Compatibility,
    DateTimeOffset UpdatedUtc)
{
    public static RuntimeEvidenceSnapshot Empty { get; } = new([], [], [], DateTimeOffset.MinValue);
}
