Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$canvas = Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeGraphCanvas.cs') -Raw

$required = @(
    'BuildConnectedComponents',
    'LayoutConnectedComponent',
    'FindStronglyConnectedComponents',
    'OptimizeLayerOrdering',
    'Barycenter',
    'targetRowWidth',
    'dynamicHorizontalGap',
    'LoadOrderRules.IsCore',
    'LoadOrderRules.IsOfficialDlc'
)
foreach ($token in $required) {
    if ($canvas -notmatch [regex]::Escape($token)) { throw "Dependency Map layout contract is missing $token." }
}
if ($canvas -match 'fallbackLevel\+\+') { throw 'Legacy one-column fallback layout remains active.' }
Write-Host 'Dependency Map layout contract test passed.'
