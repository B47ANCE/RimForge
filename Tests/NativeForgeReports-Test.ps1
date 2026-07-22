Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$runner = Join-Path $root 'src\RimForge.App\Forge\NativeForgeRunner.cs'
$analysis = Join-Path $root 'src\RimForge.Analysis\Services\ModAnalysisEngine.cs'

foreach ($path in @($runner, $analysis)) {
    if (-not (Test-Path $path)) { throw "Required native conversion file is missing: $path" }
}

$runnerText = Get-Content $runner -Raw
$analysisText = Get-Content $analysis -Raw

foreach ($required in @(
    'NativeEvidenceReport.json',
    'NativeCompatibilityReport.json',
    'ModName = mod.DisplayName',
    'SupportsTargetVersion',
    'DependencyCycles'
)) {
    if ($runnerText -notmatch [regex]::Escape($required)) {
        throw "Native Forge reports are missing required marker: $required"
    }
}

if ($analysisText -notmatch 'cycle.Select\(id => byPackageId') {
    throw 'Cycle explanations are not resolving human-readable mod names.'
}

Write-Host 'Native Forge reports test passed.'
