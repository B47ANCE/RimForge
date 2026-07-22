using System.Threading;

namespace RimForge.Core.Diagnostics;

/// <summary>
/// Provides lightweight operation correlation for future structured diagnostics.
/// Scope state flows across asynchronous calls through <see cref="AsyncLocal{T}"/>.
/// </summary>
public static class RimForgeTrace
{
    private static readonly AsyncLocal<TraceContext?> CurrentContext = new();

    public static string? CurrentOperationId => CurrentContext.Value?.OperationId;
    public static string? CurrentCategory => CurrentContext.Value?.Category;
    public static string? CurrentOperation => CurrentContext.Value?.Operation;

    public static IDisposable BeginOperation(
        string category,
        string operation,
        string? operationId = null,
        bool logLifecycle = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var previous = CurrentContext.Value;
        var context = new TraceContext(
            operationId ?? Guid.NewGuid().ToString("N"),
            category.Trim(),
            operation.Trim());

        CurrentContext.Value = context;

        if (logLifecycle)
        {
            RimForgeLogger.Write(
                RimForgeLogLevel.Debug,
                context.Category,
                $"{context.Operation} started.",
                operationId: context.OperationId);
        }

        return new OperationScope(previous, context, logLifecycle);
    }

    public static void Information(string message) =>
        WriteCurrent(RimForgeLogLevel.Information, message);

    public static void Warning(string message) =>
        WriteCurrent(RimForgeLogLevel.Warning, message);

    public static void Error(string message, Exception? exception = null) =>
        WriteCurrent(RimForgeLogLevel.Error, message, exception);

    private static void WriteCurrent(
        RimForgeLogLevel level,
        string message,
        Exception? exception = null)
    {
        var context = CurrentContext.Value;
        RimForgeLogger.Write(
            level,
            context?.Category ?? "General",
            message,
            exception,
            context?.OperationId);
    }

    private sealed record TraceContext(string OperationId, string Category, string Operation);

    private sealed class OperationScope(
        TraceContext? previous,
        TraceContext current,
        bool logLifecycle) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (logLifecycle)
            {
                RimForgeLogger.Write(
                    RimForgeLogLevel.Debug,
                    current.Category,
                    $"{current.Operation} completed.",
                    operationId: current.OperationId);
            }

            CurrentContext.Value = previous;
        }
    }
}
