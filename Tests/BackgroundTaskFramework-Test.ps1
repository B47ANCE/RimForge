$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$modelsPath = Join-Path $root 'src\RimForge.Core\BackgroundTasks\BackgroundTaskModels.cs'
$contractPath = Join-Path $root 'src\RimForge.Core\BackgroundTasks\IBackgroundTaskService.cs'
$servicePath = Join-Path $root 'src\RimForge.Infrastructure\Services\BackgroundTaskService.cs'
$compositionPath = Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs'
$windowPath = Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs'
$featureTasksPath = Join-Path $root 'src\RimForge.App\MainWindow.FeatureTasks.cs'

foreach ($path in @($modelsPath, $contractPath, $servicePath, $compositionPath, $windowPath, $featureTasksPath)) {
    if (-not (Test-Path $path)) { throw "Missing background-task framework file: $path" }
}

$models = Get-Content $modelsPath -Raw
$contract = Get-Content $contractPath -Raw
$service = Get-Content $servicePath -Raw
$composition = Get-Content $compositionPath -Raw
$window = (Get-Content $windowPath -Raw) + (Get-Content $featureTasksPath -Raw)

foreach ($token in @('BackgroundTaskState', 'BackgroundTaskProgress', 'BackgroundTaskSnapshot', 'BackgroundTaskContext')) {
    if (-not $models.Contains($token)) { throw "Missing background task model: $token" }
}

foreach ($token in @('RunAsync<T>', 'CancelCurrent', 'Report(BackgroundTaskProgress progress)', 'TaskChanged')) {
    if (-not $contract.Contains($token)) { throw "Missing background task contract member: $token" }
}

foreach ($token in @('CreateLinkedTokenSource', 'Stopwatch.StartNew', 'BackgroundTaskState.Cancelling', 'BackgroundTaskState.Completed', 'BackgroundTaskState.Cancelled', 'BackgroundTaskState.Failed')) {
    if (-not $service.Contains($token)) { throw "Missing background task lifecycle behavior: $token" }
}

foreach ($token in @('IBackgroundTaskService BackgroundTaskService', 'new BackgroundTaskService(eventBus)')) {
    if (-not $composition.Contains($token)) { throw "Background task service is not composed correctly: $token" }
}

foreach ($token in @('"library.scan"', '"forge.analysis"', 'RunFeatureTaskAsync', 'CancelFeatureTask', 'BackgroundTaskService_TaskChanged', 'new BackgroundTaskProgress(')) {
    if (-not $window.Contains($token)) { throw "Missing foreground task integration: $token" }
}

Write-Host 'Background task framework validation passed.'
