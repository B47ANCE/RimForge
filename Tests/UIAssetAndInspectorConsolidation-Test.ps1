Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'src\RimForge.App'
$ui = Join-Path $root 'src\RimForge.UI'
$mainXaml = Get-Content (Join-Path $app 'MainWindow.xaml') -Raw
$inspectorXaml = Join-Path $app 'Features\ModInspector\ModInspectorView.xaml'
$inspectorCode = Join-Path $app 'Features\ModInspector\ModInspectorView.xaml.cs'

if (-not (Test-Path $inspectorXaml) -or -not (Test-Path $inspectorCode)) {
    throw 'The Mod Inspector feature boundary is incomplete.'
}
if ($mainXaml -match 'MOD INSPECTOR' -or $mainXaml -match 'InspectorExpandedContent') {
    throw 'Mod Inspector presentation markup has drifted back into MainWindow.xaml.'
}
if ($mainXaml -notmatch 'inspector:ModInspectorView') {
    throw 'MainWindow does not host the extracted Mod Inspector view.'
}
if (Test-Path (Join-Path $app 'Assets')) {
    throw 'RimForge.App must not own a duplicate production Assets directory.'
}

$allowedRoot = (Join-Path $ui 'Assets')
$productionAssetExtensions = @('.png','.jpg','.jpeg','.webp','.svg','.ico')
$outsideAssets = Get-ChildItem (Join-Path $root 'src') -Recurse -File |
    Where-Object { $productionAssetExtensions -contains $_.Extension.ToLowerInvariant() } |
    Where-Object { -not $_.FullName.StartsWith($allowedRoot, [System.StringComparison]::OrdinalIgnoreCase) }
if ($outsideAssets) {
    throw "Production UI assets exist outside src\RimForge.UI\Assets: $($outsideAssets.FullName -join ', ')"
}

$conceptAssets = Get-ChildItem $allowedRoot -Recurse -File |
    Where-Object { $_.Name -match '(?i)concept|draft|rejected|deprecated|old|backup' }
if ($conceptAssets) {
    throw "Concept or deprecated assets remain in the production asset tree: $($conceptAssets.FullName -join ', ')"
}

$manifestPath = Join-Path $allowedRoot 'Branding\AssetManifest.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if ($manifest.assetRoot -ne 'src/RimForge.UI/Assets') {
    throw 'The asset manifest does not declare the canonical asset root.'
}

Write-Host 'UI asset and Mod Inspector consolidation test passed.'
