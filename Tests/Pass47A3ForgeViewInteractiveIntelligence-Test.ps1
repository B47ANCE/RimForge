$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$canvas = Get-Content (Join-Path $repo 'src/RimForge.App/Features/ForgeView/ForgeGraphCanvas.cs') -Raw
$view = Get-Content (Join-Path $repo 'src/RimForge.App/Features/ForgeView/ForgeViewView.xaml') -Raw
$codeBehind = Get-Content (Join-Path $repo 'src/RimForge.App/Features/ForgeView/ForgeViewView.xaml.cs') -Raw
$main = Get-Content (Join-Path $repo 'src/RimForge.App/Features/ForgeView/MainWindow.ForgeView.cs') -Raw
$evidence = Get-Content (Join-Path $repo 'src/RimForge.App/Features/SharedEvidence/MainWindow.SharedEvidence.cs') -Raw

$required = @(
    @{ Text = 'NodeHoverChanged'; Source = $canvas },
    @{ Text = 'UpdateHoveredPackage'; Source = $canvas },
    @{ Text = 'GraphCanvas_NodeHoverChanged'; Source = $codeBehind },
    @{ Text = 'SetForgeInteractionSelection'; Source = $codeBehind },
    @{ Text = 'ForgeHoverSummary'; Source = $view },
    @{ Text = 'ForgeInteractionStatus'; Source = $view },
    @{ Text = 'ForgeFocusedDependentCount'; Source = $main },
    @{ Text = 'ForgeFocusedConflictCount'; Source = $main },
    @{ Text = 'ForgeFocusedIsInCycle'; Source = $main },
    @{ Text = '_forgeGraphIntelligence = projection.Intelligence'; Source = $evidence }
)
foreach ($item in $required) {
    if (-not $item.Source.Contains($item.Text)) { throw "Pass 47A.3 gate failed: missing $($item.Text)" }
}
Write-Host 'Pass 47A.3 ForgeView interactive intelligence gate passed.' -ForegroundColor Green
