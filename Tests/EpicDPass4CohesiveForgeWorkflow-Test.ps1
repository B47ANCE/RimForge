$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$viewPath = Join-Path $root 'src/RimForge.App/Features/ForgeView/ForgeViewView.xaml'
$viewCodePath = Join-Path $root 'src/RimForge.App/Features/ForgeView/ForgeViewView.xaml.cs'
$windowPath = Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs'
$selectionPath = Join-Path $root 'src/RimForge.Core/Services/SelectionService.cs'

$view = Get-Content -Raw $viewPath
$viewCode = Get-Content -Raw $viewCodePath
$window = Get-Content -Raw $windowPath
$selection = Get-Content -Raw $selectionPath

foreach ($token in @('AutomationProperties.Name="ForgeView dependency workspace"', 'Accessible outline view',
    'WorkspaceStatePanel', 'WorkspaceStateAction', 'Keyboard: arrows pan')) {
    if (-not $view.Contains($token)) { throw "Epic D Pass 4 accessible state contract is missing: $token" }
}
foreach ($token in @('Loading dependency evidence', 'Dependency graph unavailable', 'No mods to visualize',
    'Graph uses metadata evidence', 'ForgeGraphQueryOrigin.Outline', 'window.SelectionOrigin')) {
    if (-not $viewCode.Contains($token)) { throw "Epic D Pass 4 workflow contract is missing: $token" }
}
foreach ($token in @('SelectMod(ModRecord? mod, ForgeGraphQueryOrigin origin)', 'ForgeGraphQueryOrigin.Profile',
    'Notify(nameof(SelectionOrigin))')) {
    if (-not $window.Contains($token)) { throw "Epic D Pass 4 selected-context contract is missing: $token" }
}
foreach ($token in @('public ForgeGraphQueryOrigin Origin', 'Origin = origin')) {
    if (-not $selection.Contains($token)) { throw "Epic D Pass 4 canonical selection service is missing: $token" }
}

dotnet run --project (Join-Path $root 'Tests/RimForge.ExecutionTests/RimForge.ExecutionTests.csproj') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Epic D Pass 4 execution coverage failed.' }

Write-Host 'Epic D Pass 4 cohesive ForgeView workflow verified.'
