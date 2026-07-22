$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$lifecyclePath = Join-Path $root 'src\RimForge.App\Lifecycle\ApplicationLifecycleService.cs'
$mainPath = Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs'
$lifecycle = Get-Content $lifecyclePath -Raw
$main = Get-Content $mainPath -Raw
foreach ($state in @('Created','Starting','Running','Stopping','Stopped','Failed')) {
    if (-not $lifecycle.Contains($state)) { throw "Lifecycle state missing: $state" }
}
foreach ($token in @(
    'ApplicationLifecycleState.Starting',
    'await BeginCoordinatedStartupAsync()',
    'ApplicationLifecycleState.Running',
    'ApplicationLifecycleState.Failed'
)) {
    if (-not $main.Contains($token)) { throw "Startup lifecycle wiring missing: $token" }
}
$renderIndex = $main.IndexOf('ContentRendered += async')
$startupIndex = $main.IndexOf('await BeginCoordinatedStartupAsync()', $renderIndex)
if ($renderIndex -lt 0 -or $startupIndex -lt $renderIndex) { throw 'Coordinated startup no longer begins after first render.' }
Write-Host 'Startup lifecycle validation passed.'
