using RimForge.Core.Models;

namespace RimForge.Core.Services;

public interface IPlatformValidationService
{
    Task<PlatformValidationReport> ValidateAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationRecoveryService
{
    Task<ApplicationRecoveryState> BeginRunAsync(string applicationVersion, CancellationToken cancellationToken = default);
    Task CompleteRunAsync(CancellationToken cancellationToken = default);
}

public interface IStatePreservationService
{
    Task<PreservedStateManifest> CaptureAsync(string applicationVersion, CancellationToken cancellationToken = default);
    void ValidateInstallBoundary(string installRoot);
}

public interface ISignedUpdateService
{
    bool VerifyManifest(string manifestJson, string signatureBase64, string publicKeyPem);
    Task<UpdateStagingResult> StageAsync(
        string manifestJson,
        string signatureBase64,
        string packagePath,
        CancellationToken cancellationToken = default);
    Task<UpdateRollbackResult> CaptureRollbackAsync(
        RimForgeUpdateManifest manifest,
        string installRoot,
        CancellationToken cancellationToken = default);
    Task<UpdateRollbackResult> RestoreRollbackAsync(
        string rollbackRoot,
        string installRoot,
        CancellationToken cancellationToken = default);
}
