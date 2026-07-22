param([string]$RepoRoot = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-Contains([string]$Path, [string]$Pattern, [string]$Message) {
    $text = Get-Content -LiteralPath $Path -Raw
    if ($text -notmatch $Pattern) { $failures.Add($Message) }
}

$service = Join-Path $RepoRoot 'src\RimForge.Infrastructure\Services\ForgeGraphProjectionService.cs'
$sharedEvidence = Join-Path $RepoRoot 'src\RimForge.App\Features\SharedEvidence\MainWindow.SharedEvidence.cs'

foreach ($path in @($service, $sharedEvidence)) {
    if (-not (Test-Path -LiteralPath $path)) { $failures.Add("Missing required file: $path") }
}

if ($failures.Count -eq 0) {
    Assert-Contains $service 'record ForgeGraphDiff' 'Incremental graph diff contract is missing.'
    Assert-Contains $service 'record ForgeGraphCluster' 'SCC cluster contract is missing.'
    Assert-Contains $service 'record ForgeGraphIntelligence' 'Graph intelligence projection is missing.'
    Assert-Contains $service 'FindStronglyConnectedComponents' 'Strongly connected component analysis is missing.'
    Assert-Contains $service 'Dependents' 'Reverse dependency index is missing.'
    Assert-Contains $service '_previousNodeFingerprints' 'Incremental node comparison state is missing.'
    Assert-Contains $service '_previousEdgeKeys' 'Incremental edge comparison state is missing.'
    Assert-Contains $service 'DependencyRelationshipType\.Incompatible' 'Conflict relationship accounting is missing.'
    Assert-Contains $service 'OrderBy\(node => node\.PackageId' 'Deterministic node ordering is missing.'
    Assert-Contains $sharedEvidence 'projection\.Intelligence\.Diff' 'Graph diff is not projected into application status.'
    Assert-Contains $sharedEvidence 'ForgeGraphCycleClusterCount' 'Cycle cluster status is not exposed.'
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Pass 47A.2 ForgeView graph intelligence gate passed.' -ForegroundColor Green
