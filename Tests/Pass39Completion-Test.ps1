$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not(Test-Path (Join-Path $root 'RimForge.sln'))){throw 'RimForge solution is missing.'}
foreach($path in @('src\RimForge.App\MainWindow.xaml','src\RimForge.App\MainWindow.xaml.cs','src\RimForge.App\Features\ForgeView\ForgeViewView.xaml','src\RimForge.App\Features\Search\MainWindow.Search.cs')){if(-not(Test-Path (Join-Path $root $path))){throw "Current architecture file missing: $path"}}
Write-Host ('Pass39Completion-Test: current-architecture compatibility gate passed.')
