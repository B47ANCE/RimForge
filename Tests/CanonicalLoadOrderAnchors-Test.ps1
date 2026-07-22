$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rulesPath = Join-Path $root 'src\RimForge.Core\Models\LoadOrderRules.cs'
$sorterPath = Join-Path $root 'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs'
$profilePath = Join-Path $root 'src\RimForge.Infrastructure\Services\ProfileWorkspaceService.cs'
$viewModelPath = Join-Path $root 'src\RimForge.UI\ViewModels\ProfileLoadOrderItemViewModel.cs'
foreach ($path in @($rulesPath,$sorterPath,$profilePath,$viewModelPath)) { if (-not (Test-Path $path)) { throw "Missing anchor implementation file: $path" } }
$rules = Get-Content $rulesPath -Raw
foreach ($token in @('brrainz.harmony','ludeon.rimworld','ludeon.rimworld.royalty','ludeon.rimworld.ideology','ludeon.rimworld.biotech','ludeon.rimworld.anomaly','ludeon.rimworld.odyssey','krkr.rocketman','vr.missilegirl','Normalize(IEnumerable<string> packageIds)')) {
    if (-not $rules.Contains($token)) { throw "Missing canonical load-order rule: $token" }
}
$sorter = Get-Content $sorterPath -Raw
foreach ($token in @('NormalizeActiveLoadOrder()','LoadOrderRules.IsAnchor','IsLoadOrderAnchor','Canonical load-order anchors cannot be moved','LoadOrderRules.TopAnchors','LoadOrderRules.BottomAnchors')) {
    if (-not $sorter.Contains($token)) { throw "Mod Sorter does not enforce canonical anchors: $token" }
}
$profile = Get-Content $profilePath -Raw
if (($profile | Select-String -Pattern 'LoadOrderRules.Normalize' -AllMatches).Matches.Count -lt 3) { throw 'Profile import/save/workspace paths are not normalized through LoadOrderRules.' }
$viewModel = Get-Content $viewModelPath -Raw
if (-not $viewModel.Contains('IsLoadOrderAnchor')) { throw 'Load-order rows do not expose anchor state.' }
Write-Host 'Canonical load-order anchor validation passed.'
