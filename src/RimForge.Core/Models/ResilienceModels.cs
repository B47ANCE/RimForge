namespace RimForge.Core.Models;

public enum ValidationSeverity { Information, Warning, Error }

public sealed record PlatformValidationCheck(
    string Id,
    string Component,
    ValidationSeverity Severity,
    bool Passed,
    string Message,
    string? Detail = null);

public sealed record PlatformValidationReport(
    DateTimeOffset EvaluatedAtUtc,
    IReadOnlyList<PlatformValidationCheck> Checks)
{
    public bool IsHealthy => Checks.All(check => check.Passed || check.Severity != ValidationSeverity.Error);
    public int FailureCount => Checks.Count(check => !check.Passed);
}

public sealed record ApplicationRecoveryState(
    bool PreviousShutdownWasClean,
    string CurrentRunId,
    string? InterruptedRunId,
    DateTimeOffset? InterruptedRunStartedUtc,
    string MarkerPath);

public sealed record RimForgeUpdateManifest(
    int SchemaVersion,
    string Version,
    string Channel,
    string PackageSha256,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<string> InstallFiles);

public sealed record UpdateStagingResult(
    bool Success,
    string Message,
    string? StagedPackagePath = null,
    string? TransactionPath = null);

public sealed record UpdateRollbackResult(bool Success, string Message, string? RollbackRoot = null);

public sealed record PreservedStateManifest(
    string ApplicationVersion,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> ProtectedRoots,
    IReadOnlyDictionary<string, string> CriticalFileSha256);
