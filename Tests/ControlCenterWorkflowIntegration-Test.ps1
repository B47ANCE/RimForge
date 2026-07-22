$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$main = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
$sorter = Get-Content (Join-Path $root 'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs') -Raw
$launch = Get-Content (Join-Path $root 'src\RimForge.App\Features\LaunchBar\MainWindow.LaunchBar.cs') -Raw
$settings = Get-Content (Join-Path $root 'src\RimForge.App\Features\Settings\MainWindow.Settings.cs') -Raw
$consoleXaml = Get-Content (Join-Path $root 'src\RimForge.App\Features\Console\ConsoleView.xaml') -Raw
$consoleCode = Get-Content (Join-Path $root 'src\RimForge.App\Features\Console\ConsoleView.xaml.cs') -Raw

$mainTokens = @(
    '"Mod library refreshed"',
    '"Library refresh failed"',
    '"Forge complete"',
    '"Forge failed"',
    'ActionId.Equals("view-activity"',
    'ConsoleFeature.SelectActivityTab()'
)
foreach ($token in $mainTokens) {
    if (-not $main.Contains($token)) { throw "Missing Control Center workflow token in MainWindow: $token" }
}

$sorterTokens = @(
    '"Load order saved"',
    '"Load order save failed"',
    '"Changes reverted"',
    '"Load order optimized"',
    'new NotificationAction("undo", "Undo")'
)
foreach ($token in $sorterTokens) {
    if (-not $sorter.Contains($token)) { throw "Missing Mod Sorter notification integration: $token" }
}

foreach ($token in @('"RimWorld launched"', '"Launch failed"', '"Profile activation failed"')) {
    if (-not $launch.Contains($token)) { throw "Missing launch notification integration: $token" }
}
foreach ($token in @('"Settings saved"', '"Settings save failed"')) {
    if (-not $settings.Contains($token)) { throw "Missing settings notification integration: $token" }
}
if (-not $consoleXaml.Contains('x:Name="ActivityTab"')) { throw 'Console activity tab is not addressable.' }
if (-not $consoleCode.Contains('SelectActivityTab()')) { throw 'Console activity navigation API is missing.' }

Write-Host 'Control Center workflow integration validation passed.'
