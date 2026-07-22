namespace RimForge.Core.Models;

public enum ForgeSessionStatus
{
    Idle,
    Running,
    Cancelling,
    Completed,
    Failed,
    Cancelled
}

public readonly record struct ForgeSessionId(string Value)
{
    public static ForgeSessionId New() => new(Guid.NewGuid().ToString("N"));
    public override string ToString() => Value;
}

public enum ForgeSessionState
{
    Starting,
    Running,
    Cancelling,
    Completed,
    Failed,
    Cancelled
}

public enum ForgeRuntimeStatus
{
    NotStarted,
    Starting,
    Connected,
    Disconnected,
    Stopped,
    Faulted
}

public sealed record ForgeSessionRequest(
    string Workspace,
    string? ProfileName,
    string GameVersion,
    int ModCount,
    string Message);

public sealed record ForgeSession(
    ForgeSessionId Id,
    ForgeSessionState State,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc,
    string Workspace,
    string? ProfileName,
    string GameVersion,
    int ModCount,
    ForgeRuntimeStatus RuntimeStatus,
    string Stage,
    string Message,
    string Purpose,
    int OverallProgress,
    int StageProgress,
    bool IsStageIndeterminate,
    string? Error = null)
{
    public bool IsActive => State is ForgeSessionState.Starting or ForgeSessionState.Running or ForgeSessionState.Cancelling;
}

public sealed record ForgeSessionSnapshot(
    ForgeSessionStatus Status,
    string? ProfileName,
    string Stage,
    string Message,
    string Purpose,
    int OverallProgress,
    int StageProgress,
    bool IsStageIndeterminate,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? CompletedUtc,
    string? Error = null,
    ForgeSessionId? SessionId = null,
    string? Workspace = null,
    string? GameVersion = null,
    int ModCount = 0,
    ForgeRuntimeStatus RuntimeStatus = ForgeRuntimeStatus.NotStarted)
{
    public static ForgeSessionSnapshot Idle { get; } = new(
        ForgeSessionStatus.Idle, null, "Ready", "The forge is idle.",
        "Select a profile and ignite the forge.", 0, 0, false, null, null);

    public bool IsVisible => Status != ForgeSessionStatus.Idle;
    public bool CanLaunchProfile => Status == ForgeSessionStatus.Completed && !string.IsNullOrWhiteSpace(ProfileName);
}

public enum GameLogSeverity
{
    Trace,
    Information,
    Warning,
    Error
}

public sealed record GameLogEntry(
    DateTimeOffset Timestamp,
    string Message,
    GameLogSeverity Severity)
{
    public string TimeText => Timestamp.ToLocalTime().ToString("HH:mm:ss");
}

public sealed record GameLogReplaySummary(
    string PlayerLogPath,
    long FileSizeBytes,
    int ReplayedEntries,
    int WarningEntries,
    int ErrorEntries,
    bool IncludedUnterminatedFinalLine,
    DateTimeOffset CompletedUtc)
{
    public bool HasDiagnostics => WarningEntries > 0 || ErrorEntries > 0;
}

public sealed record GameLaunchRequest(
    RimForgeProfile Profile,
    string? SteamExecutable,
    string? GameExecutable,
    string? PlayerLogPath = null);

public sealed record GameLaunchResult(
    bool Success,
    string Message,
    int? ProcessId = null,
    string? LaunchTarget = null,
    string? PlayerLogPath = null);

public sealed record LoadOrderSaveResult(
    bool Success,
    string Message,
    RimForgeProfile? UpdatedProfile = null,
    string? BackupPath = null);
