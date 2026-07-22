using System.Diagnostics;

namespace RimForge.Core.Diagnostics;

public enum RimForgeLogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

public sealed record RimForgeLogEntry(
    DateTimeOffset Timestamp,
    RimForgeLogLevel Level,
    string Category,
    string Message,
    Exception? Exception = null,
    string? OperationId = null)
{
    public override string ToString()
    {
        var operation = string.IsNullOrWhiteSpace(OperationId) ? string.Empty : $" [{OperationId}]";
        var exception = Exception is null ? string.Empty : $"{Environment.NewLine}{Exception}";
        return $"{Timestamp:O} [{Level}] [{Category}]{operation} {Message}{exception}";
    }
}

/// <summary>
/// Lightweight, dependency-free diagnostics entry point for RimForge.
/// It is intentionally opt-in: adding this type does not change application behavior
/// until a caller explicitly writes a diagnostic entry.
/// </summary>
public static class RimForgeLogger
{
    /// <summary>
    /// Raised after an entry has been written to <see cref="System.Diagnostics.Trace"/>.
    /// Subscriber failures are isolated so diagnostics cannot crash application workflows.
    /// </summary>
    public static event Action<RimForgeLogEntry>? EntryWritten;

    public static void Trace(string category, string message) =>
        Write(RimForgeLogLevel.Trace, category, message);

    public static void Debug(string category, string message) =>
        Write(RimForgeLogLevel.Debug, category, message);

    public static void Information(string category, string message) =>
        Write(RimForgeLogLevel.Information, category, message);

    public static void Warning(string category, string message) =>
        Write(RimForgeLogLevel.Warning, category, message);

    public static void Error(string category, string message, Exception? exception = null) =>
        Write(RimForgeLogLevel.Error, category, message, exception);

    public static void Critical(string category, string message, Exception? exception = null) =>
        Write(RimForgeLogLevel.Critical, category, message, exception);

    public static void Write(
        RimForgeLogLevel level,
        string category,
        string message,
        Exception? exception = null,
        string? operationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(message);

        var entry = new RimForgeLogEntry(
            DateTimeOffset.UtcNow,
            level,
            category.Trim(),
            message,
            exception,
            operationId);

        System.Diagnostics.Trace.WriteLine(entry.ToString());

        var subscribers = EntryWritten;
        if (subscribers is null)
        {
            return;
        }

        foreach (Action<RimForgeLogEntry> subscriber in subscribers.GetInvocationList())
        {
            try
            {
                subscriber(entry);
            }
            catch (Exception subscriberException)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"RimForge diagnostics subscriber failed: {subscriberException}");
            }
        }
    }
}
