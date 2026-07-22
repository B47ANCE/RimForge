using System.Diagnostics;

namespace RimForge.Core.Diagnostics;

public enum HealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}

public sealed record RuntimeHealth(
    HealthStatus Status,
    string Component,
    string Message,
    DateTimeOffset ObservedUtc,
    string? Detail = null)
{
    public static RuntimeHealth Unknown(string component) => new(
        HealthStatus.Unknown, component, "Health has not been evaluated.", DateTimeOffset.UtcNow);
}

public sealed record DiagnosticEvent(
    string EventId,
    DateTimeOffset TimestampUtc,
    RimForgeLogLevel Level,
    string Component,
    string Message,
    string? Detail = null,
    string? OperationId = null,
    string? SessionId = null,
    IReadOnlyDictionary<string, string>? Properties = null);

public sealed record PerformanceMeasurement(
    string Component,
    string Operation,
    TimeSpan Elapsed,
    DateTimeOffset CompletedUtc,
    string? OperationId = null,
    string? SessionId = null);

public interface ILogSink
{
    void Write(DiagnosticEvent diagnosticEvent);
}

public interface ISessionLog : ILogSink
{
    string? CurrentSessionId { get; }
    void BeginSession(string sessionId);
    void EndSession(string sessionId);
}

public interface IDiagnosticService : IDisposable
{
    RuntimeHealth CurrentHealth { get; }
    IReadOnlyList<DiagnosticEvent> RecentEvents { get; }
    event EventHandler<DiagnosticEvent>? EventWritten;
    event EventHandler<RuntimeHealth>? HealthChanged;
    void Write(
        RimForgeLogLevel level,
        string component,
        string message,
        Exception? exception = null,
        string? operationId = null,
        string? sessionId = null,
        IReadOnlyDictionary<string, string>? properties = null);
    void ReportHealth(RuntimeHealth health);
    IDisposable Measure(string component, string operation, string? operationId = null, string? sessionId = null);
}

public sealed class PerformanceTimer : IDisposable
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly Action<PerformanceMeasurement> _completed;
    private readonly string _component;
    private readonly string _operation;
    private readonly string? _operationId;
    private readonly string? _sessionId;
    private int _disposed;

    public PerformanceTimer(
        string component,
        string operation,
        Action<PerformanceMeasurement> completed,
        string? operationId = null,
        string? sessionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(component);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        _component = component;
        _operation = operation;
        _completed = completed ?? throw new ArgumentNullException(nameof(completed));
        _operationId = operationId;
        _sessionId = sessionId;
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stopwatch.Stop();
        _completed(new PerformanceMeasurement(
            _component, _operation, _stopwatch.Elapsed, DateTimeOffset.UtcNow, _operationId, _sessionId));
    }
}
