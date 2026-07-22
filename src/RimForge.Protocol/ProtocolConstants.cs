namespace RimForge.Protocol;

public static class ProtocolConstants
{
    public const int CurrentVersion = 2;
    public const string PipeName = "RimForge.Agent.v1";
    public const int MaximumEnvelopeBytes = 1024 * 1024;
}

public static class MessageTypes
{
    public const string Hello = "rimforge.hello";
    public const string HelloAccepted = "rimforge.hello.accepted";
    public const string Heartbeat = "rimforge.heartbeat";
    public const string RuntimeDiagnostic = "rimforge.runtime.diagnostic";
    public const string RuntimeBackfillStatus = "rimforge.runtime.backfill.status";
    public const string SessionStarted = "rimforge.session.started";
    public const string RuntimeEvidence = "rimforge.runtime.evidence";
    public const string RuntimeModInventory = "rimforge.runtime.mod-inventory";
    public const string RuntimeEvidenceBatch = "rimforge.runtime.evidence.batch";
    public const string PerformanceObservation = "rimforge.runtime.performance";
    public const string SessionEnded = "rimforge.session.ended";
}
