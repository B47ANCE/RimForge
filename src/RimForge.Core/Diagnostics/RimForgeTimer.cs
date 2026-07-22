using System.Diagnostics;
using System.Threading;

namespace RimForge.Core.Diagnostics;

/// <summary>
/// Measures an explicitly selected operation and emits one structured timing entry
/// when disposed. It has no effect unless a caller creates an instance.
/// </summary>
public sealed class RimForgeTimer : IDisposable
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly string _category;
    private readonly string _operation;
    private readonly RimForgeLogLevel _level;
    private readonly string? _operationId;
    private int _disposed;

    private RimForgeTimer(
        string category,
        string operation,
        RimForgeLogLevel level,
        string? operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        _category = category.Trim();
        _operation = operation.Trim();
        _level = level;
        _operationId = operationId;
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public static RimForgeTimer Start(
        string category,
        string operation,
        RimForgeLogLevel level = RimForgeLogLevel.Information,
        string? operationId = null) =>
        new(category, operation, level, operationId ?? RimForgeTrace.CurrentOperationId);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stopwatch.Stop();
        RimForgeLogger.Write(
            _level,
            _category,
            $"{_operation} completed in {_stopwatch.Elapsed.TotalMilliseconds:N1} ms.",
            operationId: _operationId);
    }
}
