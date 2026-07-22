using Newtonsoft.Json;

namespace RimForge.Protocol.Contracts;

public sealed class HelloPayload
{
    [JsonProperty("agentVersion")]
    public string AgentVersion { get; set; } = string.Empty;

    [JsonProperty("gameVersion")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonProperty("operatingSystem")]
    public string OperatingSystem { get; set; } = string.Empty;

    [JsonProperty("processId")]
    public int ProcessId { get; set; }

    [JsonProperty("offlineQueueAvailable")]
    public bool OfflineQueueAvailable { get; set; }

    [JsonProperty("transport")]
    public string Transport { get; set; } = "named-pipe";
}

public sealed class HeartbeatPayload
{
    [JsonProperty("sequence")]
    public long Sequence { get; set; }

    [JsonProperty("connected")]
    public bool Connected { get; set; }

    [JsonProperty("uptimeSeconds")]
    public long UptimeSeconds { get; set; }
}

public sealed class SessionEndedPayload
{
    [JsonProperty("reason")]
    public string Reason { get; set; } = "normal-shutdown";
}


public sealed class RuntimeDiagnosticPayload
{
    [JsonProperty("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("exceptionType")]
    public string ExceptionType { get; set; } = string.Empty;

    [JsonProperty("referenceId")]
    public string ReferenceId { get; set; } = string.Empty;

    [JsonProperty("stackTrace")]
    public string StackTrace { get; set; } = string.Empty;

    [JsonProperty("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;

    [JsonProperty("fingerprintVersion")]
    public int FingerprintVersion { get; set; }

    [JsonProperty("normalizedMessage")]
    public string NormalizedMessage { get; set; } = string.Empty;

    [JsonProperty("primaryFrame")]
    public string PrimaryFrame { get; set; } = string.Empty;

    [JsonProperty("canonicalMessage")]
    public string CanonicalMessage { get; set; } = string.Empty;

    [JsonProperty("primaryEvidence")]
    public string PrimaryEvidence { get; set; } = string.Empty;

    [JsonProperty("primaryEvidenceKind")]
    public string PrimaryEvidenceKind { get; set; } = string.Empty;

    [JsonProperty("occurrence")]
    public int Occurrence { get; set; }

    [JsonProperty("firstSeenUtc")]
    public System.DateTime FirstSeenUtc { get; set; }

    [JsonProperty("lastSeenUtc")]
    public System.DateTime LastSeenUtc { get; set; }

    [JsonProperty("source")]
    public string Source { get; set; } = "Player.log";
}


public sealed class RuntimeBackfillStatusPayload
{
    [JsonProperty("phase")]
    public string Phase { get; set; } = string.Empty;

    [JsonProperty("playerLogPath")]
    public string PlayerLogPath { get; set; } = string.Empty;

    [JsonProperty("pathStrategy")]
    public string PathStrategy { get; set; } = string.Empty;

    [JsonProperty("fileExists")]
    public bool FileExists { get; set; }

    [JsonProperty("fileSizeBytes")]
    public long FileSizeBytes { get; set; }

    [JsonProperty("startOffset")]
    public long StartOffset { get; set; }

    [JsonProperty("endOffset")]
    public long EndOffset { get; set; }

    [JsonProperty("bytesRead")]
    public long BytesRead { get; set; }

    [JsonProperty("linesProcessed")]
    public long LinesProcessed { get; set; }

    [JsonProperty("recordsEmitted")]
    public long RecordsEmitted { get; set; }

    [JsonProperty("recordsRejected")]
    public long RecordsRejected { get; set; }

    [JsonProperty("recordsSkipped")]
    public long RecordsSkipped { get; set; }

    [JsonProperty("detectedEncoding")]
    public string DetectedEncoding { get; set; } = string.Empty;

    [JsonProperty("pendingRecordLines")]
    public long PendingRecordLines { get; set; }

    [JsonProperty("pendingRecordBytes")]
    public long PendingRecordBytes { get; set; }

    [JsonProperty("recordsFlushedAtSnapshotEnd")]
    public long RecordsFlushedAtSnapshotEnd { get; set; }

    [JsonProperty("catchupPasses")]
    public int CatchupPasses { get; set; }

    [JsonProperty("finalStableLength")]
    public long FinalStableLength { get; set; }

    [JsonProperty("detail")]
    public string Detail { get; set; } = string.Empty;
}
