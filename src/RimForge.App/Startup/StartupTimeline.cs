using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using RimForge.App.Serialization;

namespace RimForge.App.Startup;

public sealed record StartupTimelineEvent(
    string Name,
    string Subsystem,
    DateTimeOffset Timestamp,
    double ElapsedMilliseconds,
    string? Detail);

public sealed record StartupTimelineReport(
    int ProcessId,
    DateTimeOffset ProcessStarted,
    DateTimeOffset Generated,
    double ElapsedMilliseconds,
    IReadOnlyList<StartupTimelineEvent> Events);

public static class StartupTimeline
{
    private static readonly object Gate = new();
    private static readonly List<StartupTimelineEvent> Events = [];
    private static readonly DateTimeOffset ProcessStarted = ResolveProcessStart();

    [ModuleInitializer]
    public static void Initialize() => Mark("Managed module initialized", "Runtime");

    public static void Mark(string name, string subsystem, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(subsystem);

        var now = DateTimeOffset.Now;
        var entry = new StartupTimelineEvent(
            name,
            subsystem,
            now,
            Math.Max(0, (now - ProcessStarted).TotalMilliseconds),
            detail);

        lock (Gate)
            Events.Add(entry);
    }

    public static async Task<StartupTimelineReport> WriteAsync(
        string reportPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        StartupTimelineEvent[] snapshot;
        lock (Gate)
            snapshot = Events.OrderBy(entry => entry.Timestamp).ToArray();

        var generated = DateTimeOffset.Now;
        var report = new StartupTimelineReport(
            Environment.ProcessId,
            ProcessStarted,
            generated,
            Math.Max(0, (generated - ProcessStarted).TotalMilliseconds),
            snapshot);

        var directory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            reportPath,
            JsonSerializer.Serialize(report, RimForgeJson.Indented),
            cancellationToken).ConfigureAwait(false);

        return report;
    }

    private static DateTimeOffset ResolveProcessStart()
    {
        try
        {
            return new DateTimeOffset(Process.GetCurrentProcess().StartTime);
        }
        catch
        {
            return DateTimeOffset.Now;
        }
    }
}
