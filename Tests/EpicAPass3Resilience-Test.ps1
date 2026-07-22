$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content (Join-Path $root 'src\RimForge.Core\Models\ResilienceModels.cs') -Raw
$contracts = Get-Content (Join-Path $root 'src\RimForge.Core\Services\ResilienceInterfaces.cs') -Raw
$validation = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\PlatformValidationService.cs') -Raw
$recovery = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ApplicationRecoveryService.cs') -Raw
$preservation = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\StatePreservationService.cs') -Raw
$updates = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\SignedUpdateService.cs') -Raw
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
$startup = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw

foreach ($token in @('PlatformValidationReport','ApplicationRecoveryState','RimForgeUpdateManifest','PreservedStateManifest')) {
    if (-not $models.Contains($token)) { throw "Missing resilience model: $token" }
}
foreach ($token in @('IPlatformValidationService','IApplicationRecoveryService','IStatePreservationService','ISignedUpdateService','RestoreRollbackAsync')) {
    if (-not $contracts.Contains($token)) { throw "Missing resilience contract: $token" }
}
foreach ($token in @('CheckWritableAsync','ReportHealth','ValidationSeverity.Error')) {
    if (-not $validation.Contains($token)) { throw "Missing self-validation behavior: $token" }
}
foreach ($token in @('active-run.json','InterruptedRunId','QuarantineMarker','CompleteRunAsync')) {
    if (-not (($recovery + $models).Contains($token))) { throw "Missing recovery behavior: $token" }
}
foreach ($token in @('ProtectedRoots','CriticalFileSha256','ValidateInstallBoundary','overlaps protected application state')) {
    if (-not (($preservation + $models).Contains($token))) { throw "Missing state-preservation behavior: $token" }
}
foreach ($token in @('RSASignaturePadding.Pss','_trustedChannelKeys','SHA256.HashDataAsync','ValidateRelativeFiles','CaptureRollbackAsync','RestoreRollbackAsync','ResolveWithin')) {
    if (-not $updates.Contains($token)) { throw "Missing signed-update or rollback behavior: $token" }
}
foreach ($token in @('PlatformValidationService','ApplicationRecoveryService','StatePreservationService','SignedUpdateService','await ApplicationRecoveryService.CompleteRunAsync()')) {
    if (-not $composition.Contains($token)) { throw "Missing resilience composition: $token" }
}
foreach ($token in @('Validating platform health and recovery state','BeginRunAsync','CaptureAsync')) {
    if (-not $startup.Contains($token)) { throw "Missing resilience startup integration: $token" }
}

Write-Output 'Epic A Pass 3 resilience architecture verified.'
