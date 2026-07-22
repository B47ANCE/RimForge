$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$canvasPath = Join-Path $root 'src/RimForge.App/Features/ForgeView/ForgeGraphCanvas.cs'
$viewPath = Join-Path $root 'src/RimForge.App/Features/ForgeView/ForgeViewView.xaml'
$codePath = Join-Path $root 'src/RimForge.App/Features/ForgeView/ForgeViewView.xaml.cs'
$canvas = Get-Content -Raw $canvasPath
$view = Get-Content -Raw $viewPath
$code = Get-Content -Raw $codePath

$canvasTokens = @(
    'HealthFilterProperty',
    'RelationshipFilterProperty',
    'IsolateFocusedPathProperty',
    'ApplyHealthFilter',
    'MatchesRelationshipFilter',
    'BuildFocusedNodeSet(edges)'
)
foreach ($token in $canvasTokens) {
    if (-not $canvas.Contains($token)) { throw "ForgeGraphCanvas is missing $token. Inspected: $canvasPath" }
}

$viewTokens = @(
    'HealthFilterCombo',
    'RelationshipFilterCombo',
    'IsolatePathButton',
    'GraphFilter_SelectionChanged',
    'FilterStatusText'
)
foreach ($token in $viewTokens) {
    if (-not $view.Contains($token)) { throw "ForgeViewView.xaml is missing $token. Inspected: $viewPath" }
}

$codeTokens = @(
    'SelectedTag(HealthFilterCombo',
    'SelectTag(RelationshipFilterCombo',
    'GraphCanvas.IsolateFocusedPath',
    'GraphCanvas.HealthFilter',
    'GraphCanvas.RelationshipFilter',
    'Showing {e.TotalNodes:N0} mods',
    'string HealthFilter = "All"',
    'string RelationshipFilter = "All"',
    'bool IsolateFocusedPath = false'
)
foreach ($token in $codeTokens) {
    if (-not $code.Contains($token)) { throw "ForgeViewView.xaml.cs is missing $token. Inspected: $codePath" }
}

Write-Host 'Pass 47B.2 ForgeView graph query and filtering gate passed.' -ForegroundColor Green
