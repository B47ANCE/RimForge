namespace RimForge.Core.BackgroundTasks;

public enum BackgroundTaskState
{
    Idle,
    Running,
    Cancelling,
    Completed,
    Cancelled,
    Failed
}

public sealed record BackgroundTaskProgress(
    string Stage,
    string Message,
    string TechnicalDetail,
    double? Percent = null,
    long Completed = 0,
    long Total = 0,
    string DiscoveryDetail = "",
    string CurrentFile = "")
{
    public bool IsIndeterminate => Percent is null && Total <= 0;

    public double? EffectivePercent => Percent is not null
        ? Math.Clamp(Percent.Value, 0d, 100d)
        : Total > 0
            ? Math.Clamp(Completed * 100d / Total, 0d, 100d)
            : null;
}

public sealed record BackgroundTaskSnapshot(
    string Key,
    string DisplayName,
    BackgroundTaskState State,
    BackgroundTaskProgress? Progress,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    TimeSpan Elapsed,
    string? ErrorMessage = null)
{
    public static BackgroundTaskSnapshot Idle { get; } = new(
        string.Empty,
        string.Empty,
        BackgroundTaskState.Idle,
        null,
        null,
        null,
        TimeSpan.Zero);

    public bool IsActive => State is BackgroundTaskState.Running or BackgroundTaskState.Cancelling;
}

public sealed class BackgroundTaskContext
{
    private readonly Action<BackgroundTaskProgress> _report;

    public BackgroundTaskContext(CancellationToken cancellationToken, Action<BackgroundTaskProgress> report)
    {
        CancellationToken = cancellationToken;
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public CancellationToken CancellationToken { get; }

    public void Report(BackgroundTaskProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        _report(progress);
    }
}
