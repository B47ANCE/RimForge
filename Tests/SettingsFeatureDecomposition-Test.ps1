$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$mainXaml = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml') -Raw
$mainCode = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
$view = Get-Content (Join-Path $root 'src\RimForge.App\Features\Settings\SettingsView.xaml') -Raw
$viewCode = Get-Content (Join-Path $root 'src\RimForge.App\Features\Settings\SettingsView.xaml.cs') -Raw
$featureCode = Get-Content (Join-Path $root 'src\RimForge.App\Features\Settings\MainWindow.Settings.cs') -Raw

if ($mainXaml -notmatch '<settings:SettingsView') { throw 'MainWindow does not host SettingsView.' }
foreach ($token in @('Steam Workshop Folder','Local RimWorld Mods Folder','Save Settings','Open console on game launch')) {
    if ($mainXaml.Contains($token)) { throw "Settings markup leaked into MainWindow.xaml: $token" }
    if (-not $view.Contains($token)) { throw "SettingsView is missing expected UI: $token" }
}
foreach ($token in @('SearchSteamLibrariesRequested','SaveRequested')) {
    if (-not $viewCode.Contains($token)) { throw "SettingsView is missing routed event: $token" }
}
foreach ($token in @('DiscoverSteamLibrariesAsync','SaveSettingsCoreAsync','LoadSettingsAsync','Settings_SaveRequested')) {
    $declarationPattern = '(?m)^\s*private\s+(?:async\s+)?(?:void|Task(?:<[^>]+>)?|SteamInstallationCandidate\?)\s+' + [regex]::Escape($token) + '\s*\('
    if ($mainCode -match $declarationPattern) { throw "Settings implementation remains in MainWindow.xaml.cs: $token" }
    if (-not $featureCode.Contains($token)) { throw "Settings feature code is missing: $token" }
}

foreach ($token in @('using System.IO;','using RimForge.Core.Models;')) {
    if (-not $featureCode.Contains($token)) { throw "Settings feature code is missing required import: $token" }
}
Write-Host 'Settings feature decomposition validation passed.'
