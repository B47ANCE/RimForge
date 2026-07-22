$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$mainWindow = Join-Path $root 'src\RimForge.App\MainWindow.xaml'
$app = Join-Path $root 'src\RimForge.App\App.xaml'
$commandBar = Join-Path $root 'src\RimForge.App\Features\CommandBar\EngineeringCommandBarView.xaml'
$colors = @(
    Join-Path $root 'src\RimForge.App\Themes\Colors.xaml'
    Join-Path $root 'src\RimForge.UI\Themes\Colors.xaml'
)

[xml](Get-Content -LiteralPath $mainWindow -Raw) | Out-Null
[xml](Get-Content -LiteralPath $app -Raw) | Out-Null
[xml](Get-Content -LiteralPath $commandBar -Raw) | Out-Null
foreach ($file in $colors) {
    [xml](Get-Content -LiteralPath $file -Raw) | Out-Null
}

$content = Get-Content -LiteralPath $mainWindow -Raw
$appContent = Get-Content -LiteralPath $app -Raw
$commandBarContent = Get-Content -LiteralPath $commandBar -Raw
if ($content -match 'SurfaceRaisedBrush|\{StaticResource BorderBrush\}') {
    throw 'MainWindow.xaml still references undefined legacy brush resources.'
}

if ($commandBarContent -match 'StaticResource BooleanToVisibilityConverter' -and $appContent -notmatch 'x:Key="BooleanToVisibilityConverter"') {
    throw 'Feature views reference BooleanToVisibilityConverter, but it is not available at application scope.'
}

$requiredKeys = @('Bg3Brush', 'Bg4Brush')
$themeText = ($colors | ForEach-Object { Get-Content -LiteralPath $_ -Raw }) -join "`n"
foreach ($key in $requiredKeys) {
    if ($themeText -notmatch ('x:Key="' + [regex]::Escape($key) + '"')) {
        throw "Required XAML resource '$key' was not found."
    }
}

Write-Host 'XAML resource smoke test passed.'
