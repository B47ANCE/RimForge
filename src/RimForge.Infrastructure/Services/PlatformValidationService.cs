using RimForge.Core.Diagnostics;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class PlatformValidationService : IPlatformValidationService
{
    private readonly RimForgePathLayout _paths;
    private readonly IDiagnosticService _diagnostics;

    public PlatformValidationService(RimForgePathLayout paths, IDiagnosticService diagnostics)
    {
        _paths = paths;
        _diagnostics = diagnostics;
    }

    public async Task<PlatformValidationReport> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<PlatformValidationCheck>();
        CheckFile(checks, "configuration", "Configuration", Path.Combine(_paths.RepositoryRoot, "Config.json"), true);
        CheckFile(checks, "features", "Configuration", Path.Combine(_paths.RepositoryRoot, "Features.json"), true);
        foreach (var (id, path) in new[] { ("workspace", _paths.OutputRoot), ("cache", _paths.CacheRoot), ("sessions", _paths.SessionsRoot), ("diagnostics", _paths.DiagnosticsRoot) })
            checks.Add(await CheckWritableAsync(id, path, cancellationToken).ConfigureAwait(false));

        var report = new PlatformValidationReport(DateTimeOffset.UtcNow, checks);
        var status = report.IsHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy;
        _diagnostics.ReportHealth(new RuntimeHealth(status, "PlatformValidation",
            report.IsHealthy ? "Platform self-validation passed." : "Platform self-validation found blocking failures.",
            report.EvaluatedAtUtc, $"{report.FailureCount} check(s) failed."));
        return report;
    }

    private static void CheckFile(List<PlatformValidationCheck> checks, string id, string component, string path, bool required) =>
        checks.Add(new(id, component, required ? ValidationSeverity.Error : ValidationSeverity.Warning,
            File.Exists(path), File.Exists(path) ? $"{Path.GetFileName(path)} is available." : $"{Path.GetFileName(path)} is missing.", path));

    private static async Task<PlatformValidationCheck> CheckWritableAsync(string id, string path, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, $".rimforge-health-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(probe, "health", cancellationToken).ConfigureAwait(false);
            File.Delete(probe);
            return new(id, "Workspace", ValidationSeverity.Error, true, $"{id} storage is writable.", path);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return new(id, "Workspace", ValidationSeverity.Error, false, $"{id} storage is not writable.", ex.Message); }
    }
}
