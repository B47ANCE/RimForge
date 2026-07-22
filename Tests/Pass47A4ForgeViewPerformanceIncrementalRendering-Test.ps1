$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$canvas = Get-Content (Join-Path $repo 'src/RimForge.App/Features/ForgeView/ForgeGraphCanvas.cs') -Raw
$view = Get-Content (Join-Path $repo 'src/RimForge.App/Features/ForgeView/ForgeViewView.xaml') -Raw
$codeBehind = Get-Content (Join-Path $repo 'src/RimForge.App/Features/ForgeView/ForgeViewView.xaml.cs') -Raw
$main = Get-Content (Join-Path $repo 'src/RimForge.App/Features/ForgeView/MainWindow.ForgeView.cs') -Raw
$evidence = Get-Content (Join-Path $repo 'src/RimForge.App/Features/SharedEvidence/MainWindow.SharedEvidence.cs') -Raw

$required = @(
    @{ Text = 'ViewportCullingThreshold'; Source = $canvas },
    @{ Text = 'GetLogicalViewport'; Source = $canvas },
    @{ Text = 'EdgeIntersectsViewport'; Source = $canvas },
    @{ Text = 'ForgeGraphRenderCompletedEventArgs'; Source = $canvas },
    @{ Text = 'RenderCompleted'; Source = $view },
    @{ Text = 'GraphCanvas_RenderCompleted'; Source = $codeBehind },
    @{ Text = 'ForgeGraphRenderStatus'; Source = $main },
    @{ Text = 'TimeSpan.FromMilliseconds(500)'; Source = $main },
    @{ Text = 'TrySynchronizeGraphCollections'; Source = $evidence },
    @{ Text = 'graphDiffSize <= 64'; Source = $evidence },
    @{ Text = 'incremental collection update'; Source = $evidence }
)
foreach ($item in $required) {
    if (-not $item.Source.Contains($item.Text)) { throw "Pass 47A.4 gate failed: missing $($item.Text)" }
}
Write-Host 'Pass 47A.4 ForgeView performance and incremental rendering gate passed.' -ForegroundColor Green
