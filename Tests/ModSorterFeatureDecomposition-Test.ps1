$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$view = Join-Path $root 'src\RimForge.App\Features\ModSorter\ModSorterView.xaml'
$viewCode = Join-Path $root 'src\RimForge.App\Features\ModSorter\ModSorterView.xaml.cs'
$featureCode = Join-Path $root 'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs'
$mainXaml = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml') -Raw
$mainCode = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw

foreach ($file in @($view, $viewCode, $featureCode)) {
    if (-not (Test-Path $file)) { throw "Missing Mod Sorter feature file: $file" }
}
if ($mainXaml -match 'x:Name="InactiveModsList"|x:Name="ActiveModsList"') {
    throw 'Mod Sorter list markup has drifted back into MainWindow.xaml.'
}
if ($mainCode -match 'DashboardModList_SelectionChanged|ActiveProfileMods_Drop|InactiveInstalledMods_Drop') {
    throw 'Mod Sorter interaction logic has drifted back into MainWindow.xaml.cs.'
}
if ($mainXaml -notmatch '<sorter:ModSorterView') {
    throw 'MainWindow does not host the extracted ModSorterView.'
}
Write-Host 'Mod Sorter feature decomposition test passed.'
