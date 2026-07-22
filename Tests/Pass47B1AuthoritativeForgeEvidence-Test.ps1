$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$servicePath = Join-Path $repo 'src\RimForge.Infrastructure\Services\ForgeEvidenceService.cs'
$windowPath = Join-Path $repo 'src\RimForge.App\MainWindow.xaml.cs'
$service = Get-Content -LiteralPath $servicePath -Raw
$window = Get-Content -LiteralPath $windowPath -Raw

$serviceTokens = @(
    'bool forceRescan = false',
    'forceRescan || targetChanged',
    'forceRescan ? "force" : "cached"'
)
foreach ($token in $serviceTokens) {
    if (-not $service.Contains($token)) { throw "ForgeEvidenceService is missing $token. Inspected: $servicePath" }
}

$windowTokens = @(
    '"forge.evidence"',
    'forceRescan: true',
    'ApplyForgeEvidenceSnapshot(evidenceSnapshot)',
    'Authoritative Forge evidence generation'
)
foreach ($token in $windowTokens) {
    if (-not $window.Contains($token)) { throw "MainWindow Forge flow is missing $token. Inspected: $windowPath" }
}

$evidenceIndex = $window.IndexOf('"forge.evidence"')
$analysisIndex = $window.IndexOf('"forge.analysis"', $evidenceIndex + 1)
if ($evidenceIndex -lt 0 -or $analysisIndex -lt 0 -or $evidenceIndex -ge $analysisIndex) {
    throw 'Authoritative evidence scan must execute before Forge analysis.'
}

Write-Host 'Pass 47B.1 authoritative cached Forge evidence regression gate passed.'
