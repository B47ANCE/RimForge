$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainXaml = Get-Content (Join-Path $root 'src/RimForge.App/MainWindow.xaml') -Raw
$mainCode = Get-Content (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs') -Raw
$navCodePath = Join-Path $root 'src/RimForge.App/Features/Navigation/MainWindow.Navigation.cs'
$commandBarPath = Join-Path $root 'src/RimForge.App/Features/CommandBar/EngineeringCommandBarView.xaml'
$commandBarCodePath = Join-Path $root 'src/RimForge.App/Features/CommandBar/EngineeringCommandBarView.xaml.cs'
$launchCodePath = Join-Path $root 'src/RimForge.App/Features/LaunchBar/MainWindow.LaunchBar.cs'

foreach ($path in @($navCodePath, $commandBarPath, $commandBarCodePath, $launchCodePath)) {
    if (-not (Test-Path $path)) { throw "Missing decomposed feature file: $path" }
}

if ($mainXaml -match '<features:NavigationRailView') { throw 'The retired left navigation rail is still hosted by MainWindow.' }
if ($mainXaml -notmatch 'NavigationRequested="CommandBar_NavigationRequested"') { throw 'MainWindow does not route compact command-bar navigation.' }
$commandBar = Get-Content $commandBarPath -Raw
$commandBarCode = Get-Content $commandBarCodePath -Raw
if ($commandBar -notmatch 'x:Name="NavigationMenuButton"' -or $commandBar -notmatch 'x:Name="NavigationMenuPopup"') { throw 'The compact navigation menu surface is incomplete.' }
if ($commandBarCode -notmatch 'NavigationDestination_Click' -or $commandBarCode -notmatch 'NavigationRequested') { throw 'The compact navigation event contract is incomplete.' }
if ($mainXaml -match 'x:Name="DashboardNavButton"') { throw 'Navigation button markup still lives in MainWindow.xaml.' }
if ($mainCode -match 'private async Task LaunchProfileCoreAsync') { throw 'Launch-specific workflow still lives in MainWindow.xaml.cs.' }
if ($mainCode -match 'private void UpdateNavigationState') { throw 'Navigation-specific presentation logic still lives in MainWindow.xaml.cs.' }

[xml]$commandBar | Out-Null
Write-Host 'Feature decomposition test passed.'
