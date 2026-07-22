using System.Diagnostics;
using System.IO;
using System.Text.Json;
using RimForge.App.Serialization;
using RimForge.Core.Models;

namespace RimForge.App.Startup;

public sealed record StartupStageDefinition(
    string Name,
    string Subsystem,
    Func<CancellationToken, Task> ExecuteAsync);

public sealed record StartupStageResult(
    string Name,
    string Subsystem,
    string Status,
    DateTimeOffset Started,
    DateTimeOffset Completed,
    double ElapsedMilliseconds,
    string? Error);

public sealed record StartupRunResult(
    DateTimeOffset Started,
    DateTimeOffset Completed,
    double ElapsedMilliseconds,
    IReadOnlyList<StartupStageResult> Stages,
    NativeLibraryCacheMetrics? NativeLibraryCache,
    StartupUiProjectionMetrics? UiProjection)
{
    public bool Succeeded => Stages.All(stage => stage.Status == "Completed");
}

public sealed class StartupCoordinator
{
    private readonly string _reportPath;
    private readonly Action<StartupStageDefinition>? _stageStarted;
    private readonly Action<StartupStageResult>? _stageCompleted;
    private readonly Func<NativeLibraryCacheMetrics?>? _cacheMetricsProvider;
    private readonly Func<StartupUiProjectionMetrics?>? _uiProjectionMetricsProvider;

    public StartupCoordinator(
        string reportPath,
        Action<StartupStageDefinition>? stageStarted = null,
        Action<StartupStageResult>? stageCompleted = null,
        Func<NativeLibraryCacheMetrics?>? cacheMetricsProvider = null,
        Func<StartupUiProjectionMetrics?>? uiProjectionMetricsProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        _reportPath = reportPath;
        _stageStarted = stageStarted;
        _stageCompleted = stageCompleted;
        _cacheMetricsProvider = cacheMetricsProvider;
        _uiProjectionMetricsProvider = uiProjectionMetricsProvider;
    }

    public async Task<StartupRunResult> RunAsync(
        IEnumerable<StartupStageDefinition> stages,
        CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.Now;
        var results = new List<StartupStageResult>();

        foreach (var stage in stages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _stageStarted?.Invoke(stage);
            var stageStarted = DateTimeOffset.Now;
            var stopwatch = Stopwatch.StartNew();
            StartupStageResult result;

            try
            {
                await stage.ExecuteAsync(cancellationToken).ConfigureAwait(true);
                stopwatch.Stop();
                result = new StartupStageResult(
                    stage.Name,
                    stage.Subsystem,
                    "Completed",
                    stageStarted,
                    DateTimeOffset.Now,
                    stopwatch.Elapsed.TotalMilliseconds,
                    null);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                result = new StartupStageResult(
                    stage.Name,
                    stage.Subsystem,
                    "Cancelled",
                    stageStarted,
                    DateTimeOffset.Now,
                    stopwatch.Elapsed.TotalMilliseconds,
                    "Startup was cancelled.");
                results.Add(result);
                _stageCompleted?.Invoke(result);
                await WriteReportAsync(started, results).ConfigureAwait(false);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result = new StartupStageResult(
                    stage.Name,
                    stage.Subsystem,
                    "Failed",
                    stageStarted,
                    DateTimeOffset.Now,
                    stopwatch.Elapsed.TotalMilliseconds,
                    ex.Message);
            }

            results.Add(result);
            _stageCompleted?.Invoke(result);

            if (result.Status == "Failed")
                break;
        }

        return await WriteReportAsync(started, results).ConfigureAwait(false);
    }

    private async Task<StartupRunResult> WriteReportAsync(
        DateTimeOffset started,
        IReadOnlyList<StartupStageResult> results)
    {
        var completed = DateTimeOffset.Now;
        var run = new StartupRunResult(
            started,
            completed,
            (completed - started).TotalMilliseconds,
            results.ToArray(),
            _cacheMetricsProvider?.Invoke(),
            _uiProjectionMetricsProvider?.Invoke());

        var directory = Path.GetDirectoryName(_reportPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            _reportPath,
            JsonSerializer.Serialize(run, RimForgeJson.Indented)).ConfigureAwait(false);

        return run;
    }
}
