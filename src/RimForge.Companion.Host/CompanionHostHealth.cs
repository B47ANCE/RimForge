namespace RimForge.Companion.Host;

public enum CompanionHealthStatus
{
    Starting,
    Healthy,
    Degraded,
    Stopping,
    Stopped,
    Faulted
}

public sealed record CompanionHostHealth(
    CompanionHealthStatus Status,
    string Message,
    DateTimeOffset ObservedUtc,
    bool IpcListening,
    bool AgentConnected,
    bool RimWorldRunning,
    long EnvelopesReceived,
    long RejectedEnvelopes);
