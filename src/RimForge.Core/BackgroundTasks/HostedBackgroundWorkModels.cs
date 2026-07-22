namespace RimForge.Core.BackgroundTasks;

public enum HostedBackgroundWorkState
{
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed
}

public sealed record HostedBackgroundWorkSnapshot(
    string Key,
    string DisplayName,
    HostedBackgroundWorkState State,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt = null,
    string? ErrorMessage = null)
{
    public bool IsActive => State is HostedBackgroundWorkState.Starting
        or HostedBackgroundWorkState.Running
        or HostedBackgroundWorkState.Stopping;
}
