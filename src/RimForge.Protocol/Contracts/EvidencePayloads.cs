using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RimForge.Protocol.Contracts;

public static class ForgeEvidenceProtocolSchema
{
    public const int CurrentVersion = 2;
}

public static class RuntimeEvidenceKinds
{
    public const string HarmonyConflict = "harmony-conflict";
    public const string XmlConflict = "xml-conflict";
    public const string DefConflict = "def-conflict";
    public const string RuntimeException = "runtime-exception";
    public const string ReflectionFailure = "reflection-failure";
    public const string MissingDependency = "missing-dependency";
    public const string SoftDependency = "soft-dependency";
    public const string OptionalFeature = "optional-feature";
    public const string Performance = "performance";
    public const string RuntimeWarning = "runtime-warning";
}

public static class RuntimeRelationshipKinds
{
    public const string Incompatibility = "incompatibility";
    public const string Integration = "integration";
    public const string SoftDependency = "soft-dependency";
    public const string DlcIntegration = "dlc-integration";
    public const string HarmonyInteraction = "harmony-interaction";
    public const string PerformanceInteraction = "performance-interaction";
    public const string Unknown = "unknown";
}

public sealed class RuntimeSessionStartedPayload
{
    [JsonProperty("agentVersion")] public string AgentVersion { get; set; } = string.Empty;
    [JsonProperty("gameVersion")] public string GameVersion { get; set; } = string.Empty;
    [JsonProperty("profileName")] public string ProfileName { get; set; } = string.Empty;
    [JsonProperty("environmentFingerprint")] public string EnvironmentFingerprint { get; set; } = string.Empty;
    [JsonProperty("activePackageIds")] public List<string> ActivePackageIds { get; set; } = new List<string>();
    [JsonProperty("activeDlcPackageIds")] public List<string> ActiveDlcPackageIds { get; set; } = new List<string>();
}

public sealed class RuntimeEvidencePayload
{
    [JsonProperty("evidenceId")] public string EvidenceId { get; set; } = Guid.NewGuid().ToString("D");
    [JsonProperty("fingerprint")] public string Fingerprint { get; set; } = string.Empty;
    [JsonProperty("kind")] public string Kind { get; set; } = RuntimeEvidenceKinds.RuntimeWarning;
    [JsonProperty("relationshipKind")] public string RelationshipKind { get; set; } = RuntimeRelationshipKinds.Unknown;
    [JsonProperty("severity")] public string Severity { get; set; } = "information";
    [JsonProperty("confidence")] public double Confidence { get; set; }
    [JsonProperty("sourcePackageId")] public string SourcePackageId { get; set; } = string.Empty;
    [JsonProperty("targetPackageId")] public string TargetPackageId { get; set; } = string.Empty;
    [JsonProperty("sourceAssembly")] public string SourceAssembly { get; set; } = string.Empty;
    [JsonProperty("targetAssembly")] public string TargetAssembly { get; set; } = string.Empty;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("summary")] public string Summary { get; set; } = string.Empty;
    [JsonProperty("detail")] public string Detail { get; set; } = string.Empty;
    [JsonProperty("exceptionType")] public string ExceptionType { get; set; } = string.Empty;
    [JsonProperty("stackTrace")] public string StackTrace { get; set; } = string.Empty;
    [JsonProperty("provenance")] public string Provenance { get; set; } = "runtime-sensor";
    [JsonProperty("firstObservedUtc")] public DateTime FirstObservedUtc { get; set; } = DateTime.UtcNow;
    [JsonProperty("lastObservedUtc")] public DateTime LastObservedUtc { get; set; } = DateTime.UtcNow;
    [JsonProperty("occurrenceCount")] public int OccurrenceCount { get; set; } = 1;
    [JsonProperty("attributes")] public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class RuntimeEvidenceBatchPayload
{
    [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } = 1;
    [JsonProperty("batchId")] public string BatchId { get; set; } = Guid.NewGuid().ToString("D");
    [JsonProperty("evidence")] public List<RuntimeEvidencePayload> Evidence { get; set; } = new List<RuntimeEvidencePayload>();
}

public sealed class PerformanceObservationPayload
{
    [JsonProperty("metric")] public string Metric { get; set; } = string.Empty;
    [JsonProperty("value")] public double Value { get; set; }
    [JsonProperty("unit")] public string Unit { get; set; } = string.Empty;
    [JsonProperty("sourcePackageId")] public string SourcePackageId { get; set; } = string.Empty;
    [JsonProperty("targetPackageId")] public string TargetPackageId { get; set; } = string.Empty;
    [JsonProperty("durationMilliseconds")] public double DurationMilliseconds { get; set; }
    [JsonProperty("thresholdExceeded")] public bool ThresholdExceeded { get; set; }
}

public sealed class RuntimeModInventoryEntryPayload
{
    [JsonProperty("loadOrderIndex")] public int LoadOrderIndex { get; set; }
    [JsonProperty("packageId")] public string PackageId { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("version")] public string Version { get; set; } = string.Empty;
    [JsonProperty("source")] public string Source { get; set; } = string.Empty;
    [JsonProperty("isOfficial")] public bool IsOfficial { get; set; }
    [JsonProperty("isDlc")] public bool IsDlc { get; set; }
    [JsonProperty("assemblies")] public List<string> Assemblies { get; set; } = new List<string>();
}

public sealed class RuntimeModInventoryPayload
{
    [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } = 1;
    [JsonProperty("capturedUtc")] public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    [JsonProperty("mods")] public List<RuntimeModInventoryEntryPayload> Mods { get; set; } = new List<RuntimeModInventoryEntryPayload>();
}

public sealed class ForgeEvidenceContributionPayload
{
    [JsonProperty("evidenceId")] public string EvidenceId { get; set; } = Guid.NewGuid().ToString("D");
    [JsonProperty("subjectId")] public string SubjectId { get; set; } = string.Empty;
    [JsonProperty("relatedSubjectId")] public string RelatedSubjectId { get; set; } = string.Empty;
    [JsonProperty("evidenceType")] public string EvidenceType { get; set; } = string.Empty;
    [JsonProperty("summary")] public string Summary { get; set; } = string.Empty;
    [JsonProperty("confidence")] public double Confidence { get; set; }
    [JsonProperty("sourceKind")] public string SourceKind { get; set; } = string.Empty;
    [JsonProperty("sourceId")] public string SourceId { get; set; } = string.Empty;
    [JsonProperty("sourceVersion")] public string SourceVersion { get; set; } = string.Empty;
    [JsonProperty("observedAtUtc")] public DateTime ObservedAtUtc { get; set; } = DateTime.UtcNow;
    [JsonProperty("firstObservedAtUtc")] public DateTime FirstObservedAtUtc { get; set; } = DateTime.UtcNow;
    [JsonProperty("lastObservedAtUtc")] public DateTime LastObservedAtUtc { get; set; } = DateTime.UtcNow;
    [JsonProperty("observationCount")] public int ObservationCount { get; set; } = 1;
    [JsonProperty("attributes")] public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class ForgeEvidenceIngestionBatchPayload
{
    [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } = ForgeEvidenceProtocolSchema.CurrentVersion;
    [JsonProperty("batchId")] public string BatchId { get; set; } = Guid.NewGuid().ToString("D");
    [JsonProperty("sourceKind")] public string SourceKind { get; set; } = string.Empty;
    [JsonProperty("createdAtUtc")] public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [JsonProperty("contributions")] public List<ForgeEvidenceContributionPayload> Contributions { get; set; } = new List<ForgeEvidenceContributionPayload>();
}
