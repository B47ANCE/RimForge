param([string]$RepoRoot = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-Contains([string]$Path, [string]$Pattern, [string]$Message) {
    $text = Get-Content -LiteralPath $Path -Raw
    if ($text -notmatch $Pattern) { $failures.Add($Message) }
}

$service = Join-Path $RepoRoot 'src\RimForge.Infrastructure\Services\ForgeGraphProjectionService.cs'
$composition = Join-Path $RepoRoot 'src\RimForge.App\Composition\RimForgeApplicationServices.cs'
$sharedEvidence = Join-Path $RepoRoot 'src\RimForge.App\Features\SharedEvidence\MainWindow.SharedEvidence.cs'

foreach ($path in @($service, $composition, $sharedEvidence)) {
    if (-not (Test-Path -LiteralPath $path)) { $failures.Add("Missing required file: $path") }
}

if ($failures.Count -eq 0) {
    Assert-Contains $service 'interface IForgeGraphProjectionService' 'Projection service contract is missing.'
    Assert-Contains $service 'ForgeEvidenceSnapshot evidenceSnapshot' 'Projection is not grounded in Shared Evidence generations.'
    Assert-Contains $service 'ReusedNodes' 'Incremental graph node reuse metrics are missing.'
    Assert-Contains $service 'TopologySignature' 'Deterministic topology signature is missing.'
    Assert-Contains $service 'SHA256\.HashData' 'Topology signature is not content-derived.'
    Assert-Contains $service '_nodeCache' 'Projection node cache is missing.'
    Assert-Contains $composition 'ForgeGraphProjectionService' 'Projection service is not registered in the composition root.'
    Assert-Contains $sharedEvidence '_forgeGraphProjectionService\.Project' 'Shared Evidence publication does not refresh ForgeView.'
    Assert-Contains $sharedEvidence 'DependencyNodes\.ReplaceAll' 'ForgeView node publication is not atomic.'
    Assert-Contains $sharedEvidence 'DependencyEdges\.ReplaceAll' 'ForgeView edge publication is not atomic.'
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Pass 47A.1 Shared Evidence graph projection gate passed.' -ForegroundColor Green
