$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$canvas = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Features/ForgeView/ForgeGraphCanvas.cs')
$view = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Features/ForgeView/ForgeViewView.xaml')
$codeBehind = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Features/ForgeView/ForgeViewView.xaml.cs')

$requiredCanvas = @(
    'IsLayoutEditModeProperty',
    'ForgeGraphLayoutChangedEventArgs',
    '_customPositions',
    '_pinnedPackageIds',
    'TogglePin',
    'ResetCustomLayout',
    'ApplyLayoutState',
    'Color.FromRgb(184, 134, 62)'
)
foreach ($token in $requiredCanvas) {
    if (-not $canvas.Contains($token)) { throw "ForgeGraphCanvas is missing $token" }
}

$requiredView = @('Edit Layout: Off', 'Pin Node', 'Reset Layout', 'LayoutChanged="GraphCanvas_LayoutChanged"')
foreach ($token in $requiredView) {
    if (-not $view.Contains($token)) { throw "ForgeViewView.xaml is missing $token" }
}

$requiredPersistence = @(
    'ForgeView.layout.json',
    'JsonSerializer.Deserialize<ForgeLayoutDocument>',
    'File.Move(temporaryPath, path, true)',
    'window.SelectedProfile.IsLocked',
    'Window_PropertyChanged',
    'TimeSpan.FromMilliseconds(350)'
)
foreach ($token in $requiredPersistence) {
    if (-not $codeBehind.Contains($token)) { throw "ForgeViewView.xaml.cs is missing $token" }
}

Write-Host 'Pass 47B.1 ForgeView profile-owned layout gate passed.' -ForegroundColor Green
