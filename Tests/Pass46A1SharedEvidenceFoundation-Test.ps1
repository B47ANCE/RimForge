$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$servicePath = Join-Path $root 'src/RimForge.Infrastructure/Services/ForgeEvidenceService.cs'
$compositionPath = Join-Path $root 'src/RimForge.App/Composition/RimForgeApplicationServices.cs'
$busPath = Join-Path $root 'src/RimForge.Infrastructure/Services/ForgeEvidenceBus.cs'

if (-not (Test-Path $servicePath)) { throw 'ForgeEvidenceService.cs is missing.' }
$service = Get-Content $servicePath -Raw
$composition = Get-Content $compositionPath -Raw
$bus = Get-Content $busPath -Raw

$contracts = @(
    'public interface IForgeEvidenceService',
    'public sealed class ForgeEvidenceSnapshot',
    'public sealed record ForgeEvidenceMetrics',
    'SemaphoreSlim _schedulerGate',
    'Parallel.ForEachAsync',
    'TargetVersionChanged',
    'FileSystemWatcher',
    'WatcherOverflow',
    'CancelCurrent()'
)

foreach ($contract in $contracts) {
    if (-not $service.Contains($contract)) { throw "Missing Shared Evidence contract: $contract" }
}

foreach ($contract in @('IForgeEvidenceBus', 'ForgeEvidencePublication', 'event EventHandler<ForgeEvidencePublication>? Published')) {
    if (-not $bus.Contains($contract)) { throw "Missing Shared Evidence bus contract: $contract" }
}

if (-not $composition.Contains('IForgeEvidenceService ForgeEvidenceService')) {
    throw 'Shared Evidence service is not exposed by application composition.'
}
if (-not $composition.Contains('IForgeEvidenceBus ForgeEvidenceBus')) {
    throw 'Shared Evidence bus is not exposed by application composition.'
}
if ($composition -notmatch 'new\s+ForgeEvidenceService\s*\(') {
    throw 'Shared Evidence service is not created by application composition.'
}
if (-not $composition.Contains('await ForgeEvidenceService.DisposeAsync()')) {
    throw 'Shared Evidence service is not disposed during shutdown.'
}

Write-Host 'Pass46A1SharedEvidenceFoundation-Test: PASSED'
