$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$canvasPath = Join-Path $root 'src/RimForge.App/Features/ForgeView/ForgeGraphCanvas.cs'
$mainPath = Join-Path $root 'src/RimForge.App/Features/ForgeView/MainWindow.ForgeView.cs'
$canvas = Get-Content -Raw $canvasPath
$main = Get-Content -Raw $mainPath

foreach ($token in @(
    'AsyncLayoutThreshold',
    'IncrementalLayoutChangeLimit',
    'MaxLayoutCacheEntries = 8',
    'ScheduleLayout(',
    'BuildLayoutAsync(',
    'CancellationTokenSource',
    'generation != _layoutGeneration',
    'ReuseStablePositions(',
    'while (_layoutResults.Count > MaxLayoutCacheEntries',
    'ViewportCullingThreshold',
    'ForgeGraphPerformanceBudgets',
    'RepresentativeNodeCount = 1000',
    'LayoutBudget',
    'RenderBudget')) {
    if (-not $canvas.Contains($token)) { throw "Epic D Pass 3 canvas contract is missing: $token" }
}
foreach ($token in @('LayoutPending', 'LayoutCacheHit', 'LayoutGeneration', 'over render budget')) {
    if (-not $main.Contains($token)) { throw "Epic D Pass 3 telemetry is missing: $token" }
}

dotnet run --project (Join-Path $root 'Tests/RimForge.ForgeViewPerformanceTests/RimForge.ForgeViewPerformanceTests.csproj') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'ForgeView representative performance fixture failed.' }

Write-Host 'Epic D Pass 3 scalable graph rendering verified.'
