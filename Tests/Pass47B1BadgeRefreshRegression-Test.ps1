$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sharedEvidencePath = Join-Path $repoRoot 'src\RimForge.App\Features\SharedEvidence\MainWindow.SharedEvidence.cs'
$sorterVmPath = Join-Path $repoRoot 'src\RimForge.UI\ViewModels\ModSorterItemViewModel.cs'
$profileVmPath = Join-Path $repoRoot 'src\RimForge.UI\ViewModels\ProfileLoadOrderItemViewModel.cs'

foreach ($path in @($sharedEvidencePath, $sorterVmPath, $profileVmPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing required file: $path" }
}

$sharedEvidence = Get-Content -LiteralPath $sharedEvidencePath -Raw
$sorterVm = Get-Content -LiteralPath $sorterVmPath -Raw
$profileVm = Get-Content -LiteralPath $profileVmPath -Raw

foreach ($token in @(
    'var evidenceChangedMods = new List<ModRecord>();',
    'evidenceChangedMods.Add(mod);',
    'ApplyBackgroundIntelligenceUpdate(mod);'
)) {
    if (-not $sharedEvidence.Contains($token)) {
        throw "Shared Evidence badge refresh path is missing: $token"
    }
}

if (-not $sorterVm.Contains('NotifyEvidenceChanged()')) {
    throw 'ModSorterItemViewModel is missing NotifyEvidenceChanged().'
}
if (-not $profileVm.Contains('NotifyEvidenceChanged()')) {
    throw 'ProfileLoadOrderItemViewModel is missing NotifyEvidenceChanged().'
}
if ($sharedEvidence.Contains('RebuildModSorter();') -and $sharedEvidence.IndexOf('RebuildModSorter();') -lt $sharedEvidence.IndexOf('private bool TrySynchronizeGraphCollections')) {
    throw 'ApplyForgeEvidenceSnapshot must not rebuild the complete Mod Sorter to refresh badges.'
}

Write-Host 'Pass 47B.1 badge refresh regression gate passed.' -ForegroundColor Green
