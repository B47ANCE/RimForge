$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$xamlPath = Join-Path $root 'src\RimForge.App\MainWindow.xaml'
$xaml = Get-Content $xamlPath -Raw

$required = @(
    'Grid.Row="0" Grid.RowSpan="4"',
    'x:Name="WorkspaceScrollViewer"',
    'Margin="0,92,0,118"',
    'x:Name="EngineeringCommandBar"',
    'Panel.ZIndex="60"',
    'x:Name="ControlCenter" Grid.Row="3" Grid.ColumnSpan="2" Panel.ZIndex="50"'
)
foreach ($token in $required) {
    if (-not $xaml.Contains($token)) { throw "Missing overlay viewport token: $token" }
}

if ($xaml -match '<ScrollViewer[^>]+Grid.Row="1"') {
    throw 'Workspace ScrollViewer is still confined to the middle content row.'
}
if ($xaml -match '<Grid[^>]+Grid.Row="1"[^>]+WorkspaceScrollViewer') {
    throw 'Workspace host is still framed between the command and launch bars.'
}

Write-Host 'Pass45A7OverlayViewportHotfix-Test: PASSED' -ForegroundColor Green
