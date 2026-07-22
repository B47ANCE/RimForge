$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
$main = (Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw) +
        (Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.FeatureTasks.cs') -Raw)
foreach ($token in @(
    'BackgroundTaskService.CancelCurrent("Application shutdown requested.")',
    'ForgeSessionService.Cancel("Application shutdown requested.")',
    'await GameLogService.StopAsync()',
    'await GameLogService.DisposeAsync()',
    'ApplicationLifecycleState.Stopping',
    'ApplicationLifecycleState.Stopped'
)) {
    if (-not $composition.Contains($token)) { throw "Shutdown composition is missing: $token" }
}
foreach ($token in @('CancelFeatureTask("Application shutdown requested.")', 'subscription.Dispose()', '_undoService.StateChanged -= UndoService_StateChanged')) {
    if (-not $main.Contains($token)) { throw "Window shutdown cleanup is missing: $token" }
}
Write-Host 'Shutdown lifecycle validation passed.'
