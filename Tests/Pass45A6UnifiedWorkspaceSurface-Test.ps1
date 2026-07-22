$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$mainXamlPath = Join-Path $root 'src\RimForge.App\MainWindow.xaml'
$mainCodePath = Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs'
$textureXamlPath = Join-Path $root 'src\RimForge.App\Features\TextureTools\TextureToolsView.xaml'
$forgeXamlPath = Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml'

$mainXaml = Get-Content $mainXamlPath -Raw
$mainCode = Get-Content $mainCodePath -Raw
$textureXaml = Get-Content $textureXamlPath -Raw
$forgeXaml = Get-Content $forgeXamlPath -Raw

$mainRequired = @(
    'x:Name="DashboardPanel"',
    'Background="{DynamicResource Bg0Brush}"',
    'x:Name="ForgeViewPanel"',
    'x:Name="WorkspaceMetricsPanel"',
    'x:Name="TextureToolsPanel"',
    'texturetools:TextureToolsView',
    'x:Name="SettingsPanel" Style="{StaticResource Card}"',
    'x:Name="IssueViewerWorkspacePanel" MinHeight="0"'
)
foreach ($token in $mainRequired) {
    if (-not $mainXaml.Contains($token)) { throw "Missing expected Commit 11 main surface token: $token" }
}

foreach ($token in @('Text="Texture Conversion Tools"', 'Text="CONVERSION SETTINGS"', 'Content="Convert Selected"', 'Content="Convert All Eligible"', 'Content="Revert Conversion"')) {
    if (-not $textureXaml.Contains($token)) { throw "Missing expected Texture Tools surface token: $token" }
}

foreach ($token in @('Text="SELECTION CONTEXT"', 'x:Name="GraphViewport"', 'Grid.Row="1" Margin="0,12,0,0"')) {
    if (-not $forgeXaml.Contains($token)) { throw "Missing expected ForgeView layout token: $token" }
}

if (-not $mainCode.Contains('IssueViewerWorkspacePanel.MinHeight = 0;')) {
    throw 'Issue Viewer forced-height removal was not found.'
}
if ($mainCode.Contains('IssueViewerWorkspacePanel.MinHeight = Math.Max(360, viewportHeight * 0.72);')) {
    throw 'Legacy Issue Viewer percentage-height floor is still present.'
}
Write-Host 'Pass45A6UnifiedWorkspaceSurface-Test: PASSED' -ForegroundColor Green
