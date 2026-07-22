namespace RimForge.Core.Models;

public enum ActivitySeverity
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record ActivityEntry(
    DateTimeOffset Timestamp,
    ActivitySeverity Severity,
    string Message,
    string? Detail = null)
{
    public string TimeText => Timestamp.ToLocalTime().ToString("HH:mm:ss");
    public string Symbol => Severity switch
    {
        ActivitySeverity.Success => "✓",
        ActivitySeverity.Warning => "!",
        ActivitySeverity.Error => "×",
        _ => "•"
    };
}
