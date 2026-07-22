using System.Text.Json;
using System.Text.Json.Serialization;
using RimForge.Core.Diagnostics;

namespace RimForge.Infrastructure.Services;

public sealed class JsonlLogSink : ILogSink, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly object _gate = new();
    private readonly StreamWriter _writer;

    public JsonlLogSink(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        _writer = new StreamWriter(new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
    }

    public string Path { get; }
    public void Write(DiagnosticEvent diagnosticEvent)
    {
        var json = JsonSerializer.Serialize(diagnosticEvent, JsonOptions);
        lock (_gate) _writer.WriteLine(json);
    }
    public void Dispose() { lock (_gate) _writer.Dispose(); }
}

public sealed class SessionLog : ISessionLog, IDisposable
{
    private readonly object _gate = new();
    private readonly string _sessionsRoot;
    private JsonlLogSink? _sink;
    public SessionLog(string sessionsRoot) => _sessionsRoot = System.IO.Path.GetFullPath(sessionsRoot);
    public string? CurrentSessionId { get; private set; }

    public void BeginSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            _sink?.Dispose();
            CurrentSessionId = sessionId;
            _sink = new JsonlLogSink(System.IO.Path.Combine(_sessionsRoot, sessionId, "session-log.jsonl"));
        }
    }

    public void EndSession(string sessionId)
    {
        lock (_gate)
        {
            if (!string.Equals(CurrentSessionId, sessionId, StringComparison.Ordinal)) return;
            _sink?.Dispose();
            _sink = null;
            CurrentSessionId = null;
        }
    }

    public void Write(DiagnosticEvent diagnosticEvent)
    {
        lock (_gate)
        {
            if (_sink is null || !string.Equals(diagnosticEvent.SessionId, CurrentSessionId, StringComparison.Ordinal)) return;
            _sink.Write(diagnosticEvent);
        }
    }

    public void Dispose()
    {
        lock (_gate) { _sink?.Dispose(); _sink = null; CurrentSessionId = null; }
    }
}

public sealed class DiagnosticService : IDiagnosticService
{
    private const int RecentEventLimit = 500;
    private readonly object _gate = new();
    private readonly List<ILogSink> _sinks;
    private readonly Queue<DiagnosticEvent> _recent = new();
    private RuntimeHealth _health = RuntimeHealth.Unknown("RimForge");
    private int _disposed;

    public DiagnosticService(params ILogSink[] sinks)
    {
        _sinks = sinks?.Distinct().ToList() ?? [];
        RimForgeLogger.EntryWritten += LoggerEntryWritten;
    }

    public RuntimeHealth CurrentHealth { get { lock (_gate) return _health; } }
    public IReadOnlyList<DiagnosticEvent> RecentEvents { get { lock (_gate) return _recent.ToArray(); } }
    public event EventHandler<DiagnosticEvent>? EventWritten;
    public event EventHandler<RuntimeHealth>? HealthChanged;

    public void Write(
        RimForgeLogLevel level,
        string component,
        string message,
        Exception? exception = null,
        string? operationId = null,
        string? sessionId = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        var diagnosticEvent = new DiagnosticEvent(
            Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, level,
            component, message, exception?.ToString(), operationId, sessionId, properties);
        lock (_gate)
        {
            _recent.Enqueue(diagnosticEvent);
            while (_recent.Count > RecentEventLimit) _recent.Dequeue();
        }
        foreach (var sink in _sinks)
        {
            try { sink.Write(diagnosticEvent); }
            catch (Exception sinkException) { System.Diagnostics.Trace.WriteLine(sinkException); }
        }
        EventWritten?.Invoke(this, diagnosticEvent);
    }

    public void ReportHealth(RuntimeHealth health)
    {
        lock (_gate) _health = health;
        HealthChanged?.Invoke(this, health);
        Write(
            health.Status is HealthStatus.Unhealthy ? RimForgeLogLevel.Error :
            health.Status is HealthStatus.Degraded ? RimForgeLogLevel.Warning : RimForgeLogLevel.Information,
            health.Component,
            health.Message,
            properties: health.Detail is null ? null : new Dictionary<string, string> { ["detail"] = health.Detail });
    }

    public IDisposable Measure(string component, string operation, string? operationId = null, string? sessionId = null) =>
        new PerformanceTimer(component, operation, measurement => Write(
            RimForgeLogLevel.Information,
            measurement.Component,
            $"{measurement.Operation} completed in {measurement.Elapsed.TotalMilliseconds:N1} ms.",
            operationId: measurement.OperationId,
            sessionId: measurement.SessionId,
            properties: new Dictionary<string, string>
            {
                ["operation"] = measurement.Operation,
                ["elapsedMilliseconds"] = measurement.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
            }), operationId, sessionId);

    private void LoggerEntryWritten(RimForgeLogEntry entry) => Write(
        entry.Level, entry.Category, entry.Message, entry.Exception, entry.OperationId);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        RimForgeLogger.EntryWritten -= LoggerEntryWritten;
        foreach (var sink in _sinks.OfType<IDisposable>()) sink.Dispose();
    }
}
