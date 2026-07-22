$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$required = @(
    'src\RimForge.Core\Models\ProfileModels.cs',
    'src\RimForge.Core\Services\NativeEngineInterfaces.cs',
    'src\RimForge.Infrastructure\Services\ProfileWorkspaceService.cs',
    'src\RimForge.App\MainWindow.xaml.cs'
)
foreach ($relative in $required) {
    if (-not (Test-Path (Join-Path $root $relative))) { throw "Missing Profile Management Suite file: $relative" }
}

$models = Get-Content (Join-Path $root 'src\RimForge.Core\Models\ProfileModels.cs') -Raw
foreach ($token in @('ProfileOperationKind','ProfileOperationResult','ProfileComparisonResult','ProfileOrderChange','ProfileBackupManifest','ModsConfigSha256','CanRestore')) {
    if (-not $models.Contains($token)) { throw "Missing profile management contract: $token" }
}

$interfaces = Get-Content (Join-Path $root 'src\RimForge.Core\Services\NativeEngineInterfaces.cs') -Raw
foreach ($token in @('CreateAsync','DuplicateAsync','RenameAsync','DeleteAsync','ImportAsync','ExportAsync','RestoreAsync','RestoreActivationRecoveryAsync','ProfileComparisonResult Compare')) {
    if (-not $interfaces.Contains($token)) { throw "Missing profile workspace service contract: $token" }
}

$service = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ProfileWorkspaceService.cs') -Raw
foreach ($token in @(
    'WriteSourceProfileAtomicAsync',
    'DeleteFileVerified',
    'DeleteDirectoryVerified',
    'CreateTransactionRoot',
    'MoveFileVerified',
    'MoveDirectoryVerified',
    'TryRestoreMovedFile',
    'TryRestoreMovedDirectory',
    'original profile was preserved',
    'CreateBackupAsync',
    'ModsConfig.RimForgeRecovery.xml',
    'RimForgeRestore.tmp',
    'RestoreActivationRecoveryAsync',
    'File.Move(temporaryPath, path, true)',
    'ProfileOperationKind.Duplicate',
    'ProfileOperationKind.Restore',
    'LoadOrderRules.Normalize',
    'profile.IsBuiltIn || profile.IsLocked',
    'ProfileComparisonResult',
    'DistinctPackageIds',
    'CleanupStaleTransactions',
    'DateTime.UtcNow.AddDays(-7)',
    'SHA256.HashData',
    'failed its integrity check'
)) {
    if (-not $service.Contains($token)) { throw "Missing authoritative profile behavior: $token" }
}

$app = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
foreach ($token in @(
    '_profileWorkspaceService.CreateAsync',
    '_profileWorkspaceService.DuplicateAsync',
    '_profileWorkspaceService.RenameAsync',
    '_profileWorkspaceService.ImportAsync',
    '_profileWorkspaceService.ExportAsync',
    '_profileWorkspaceService.DeleteAsync',
    '_profileWorkspaceService.RestoreAsync',
    'CompleteProfileOperationAsync',
    'Unsaved Profile Changes',
    'restore-profile-backup',
    'restore-activation-recovery',
    'RestorePendingActivationRecoveryAsync',
    'open-profile-export',
    '_favoriteProfileNames.RemoveWhere',
    '_lockedProfileNames.RemoveWhere'
)) {
    if (-not $app.Contains($token)) { throw "Missing profile workflow integration: $token" }
}

Write-Host 'Profile Management Suite validation passed.' -ForegroundColor Green
