namespace RimForge.Core.Models;

public enum CompanionHostProcessState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Faulted
}

public sealed record CompanionHostLaunchRequest(
    ForgeSessionId ForgeSessionId,
    string StateRoot,
    string? PlayerLogPath = null,
    int? RimWorldProcessId = null,
    string? ExecutablePath = null,
    string? PipeName = null);

public sealed record CompanionHostProcessSnapshot(
    CompanionHostProcessState State,
    int? ProcessId,
    ForgeSessionId? ForgeSessionId,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? StoppedUtc,
    string Message,
    string? Error = null)
{
    public static CompanionHostProcessSnapshot Stopped { get; } = new(
        CompanionHostProcessState.Stopped, null, null, null, null, "Companion Host is stopped.");
}
