$fixture = Join-Path (Split-Path -Parent $PSScriptRoot) 'Output/Reports/StartupMetrics.json'
if (-not (Test-Path $fixture)) { Write-Host 'Startup metrics fixture is not present; run RimForge startup before this runtime test.'; exit 0 } # prerequisite skip
param(
    [string]$Path = (Join-Path $PSScriptRoot '..\Output\Reports\StartupMetrics.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Startup metrics not found: $Path"
}

$metrics = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
if ($null -eq $metrics.NativeLibraryCache) { throw 'NativeLibraryCache metrics are missing.' }
if ($null -eq $metrics.UiProjection) { throw 'UiProjection metrics are missing.' }

$requiredCache = @('DiscoveryMilliseconds','MaterializationMilliseconds','ValidationMilliseconds','DependencyGraphMilliseconds','ServiceTotalMilliseconds')
foreach ($name in $requiredCache) {
    if ($null -eq $metrics.NativeLibraryCache.$name) { throw "NativeLibraryCache.$name is missing." }
}

$requiredUi = @('PreliminaryRecordsPublished','PreliminaryProjectionMilliseconds','AnalysisMilliseconds','SorterProjectionBuildMilliseconds','ModsCollectionMilliseconds','DependencyEdgesCollectionMilliseconds','ModSorterCollectionMilliseconds','ProfileLoadMilliseconds','TotalApplySnapshotMilliseconds')
foreach ($name in $requiredUi) {
    if ($null -eq $metrics.UiProjection.$name) { throw "UiProjection.$name is missing." }
}

Write-Host 'Startup projection metrics test passed.'
Write-Host ("ServiceTotal={0:N1} ms; UiProjectionTotal={1:N1} ms; Preliminary={2:N1} ms" -f $metrics.NativeLibraryCache.ServiceTotalMilliseconds, $metrics.UiProjection.TotalApplySnapshotMilliseconds, $metrics.UiProjection.PreliminaryProjectionMilliseconds)
