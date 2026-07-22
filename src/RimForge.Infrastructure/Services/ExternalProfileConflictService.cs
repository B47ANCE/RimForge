using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class ExternalProfileConflictService(IProfileWorkspaceService profileWorkspaceService)
    : IExternalProfileConflictService
{
    public async Task<ExternalProfileResolutionResult> ResolveAsync(
        ExternalProfileReconciliation reconciliation,
        ExternalProfileResolution resolution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reconciliation);
        cancellationToken.ThrowIfCancellationRequested();

        if (reconciliation.IsIdentical)
            return new(true, resolution, "The external configuration already matches the RimForge profile.");

        if (resolution == ExternalProfileResolution.Defer)
            return new(true, resolution, "External profile reconciliation was deferred. No files were changed.");

        if (resolution == ExternalProfileResolution.AdoptExternal)
        {
            if (reconciliation.Profile.IsLocked)
                return new(false, resolution, $"Profile '{reconciliation.Profile.Name}' is locked and cannot adopt external changes.");
            var save = await profileWorkspaceService.SaveLoadOrderAsync(
                reconciliation.Profile,
                reconciliation.External.ActiveMods,
                cancellationToken).ConfigureAwait(false);
            return new(
                save.Success,
                resolution,
                save.Message,
                save.UpdatedProfile,
                save.BackupPath,
                save.Success);
        }

        var activation = await profileWorkspaceService.ActivateAsync(reconciliation.Profile, cancellationToken).ConfigureAwait(false);
        return new(
            activation.Success,
            resolution,
            activation.Message,
            reconciliation.Profile,
            activation.RecoveryPath,
            activation.Success);
    }
}
