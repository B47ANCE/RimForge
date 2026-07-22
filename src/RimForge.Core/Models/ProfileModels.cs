namespace RimForge.Core.Models;

public sealed record RimForgeProfile(
    string Name,
    string WorkspacePath,
    string ModsConfigPath,
    IReadOnlyList<string> ActiveMods,
    IReadOnlyList<string> KnownExpansions,
    string Version,
    bool IsBuiltIn,
    bool IsLocked,
    DateTimeOffset? LastGenerated = null)
{
    public int ModCount => ActiveMods.Count;
    public string LockLabel => IsLocked ? "Locked" : "Editable";
}

public sealed record ProfileActivationResult(
    bool Success,
    string Message,
    string? ActiveModsConfigPath = null,
    string? RecoveryPath = null);

public enum ProfileOperationKind
{
    Create,
    Duplicate,
    Rename,
    Import,
    Export,
    Delete,
    Restore
}

public sealed record ProfileOperationResult(
    bool Success,
    ProfileOperationKind Operation,
    string Message,
    RimForgeProfile? Profile = null,
    string? BackupPath = null,
    string? ExportPath = null,
    bool CanRestore = false);

public sealed record ProfileComparisonResult(
    string LeftProfileName,
    string RightProfileName,
    IReadOnlyList<string> AddedPackageIds,
    IReadOnlyList<string> RemovedPackageIds,
    IReadOnlyList<ProfileOrderChange> OrderChanges)
{
    public bool IsIdentical => AddedPackageIds.Count == 0 && RemovedPackageIds.Count == 0 && OrderChanges.Count == 0;
}

public sealed record ProfileOrderChange(
    string PackageId,
    int LeftIndex,
    int RightIndex);

public sealed record ProfileBackupManifest(
    string ProfileName,
    string Version,
    DateTimeOffset CreatedUtc,
    IReadOnlyList<string> ActiveMods,
    IReadOnlyList<string> KnownExpansions,
    string ModsConfigFile = "ModsConfig.xml",
    string? ModsConfigSha256 = null);
