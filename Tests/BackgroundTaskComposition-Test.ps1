$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
$main = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
if (-not $composition.Contains('new BackgroundTaskService(eventBus)')) { throw 'Background task service is not composed with the event bus.' }
if (-not $main.Contains('_backgroundTaskService = services.BackgroundTaskService;')) { throw 'MainWindow does not consume the composed background task service.' }
if (-not $composition.Contains('BackgroundTaskService.CancelCurrent("Application shutdown requested.")')) { throw 'Background tasks are not cancelled during shutdown.' }
Write-Host 'Background task composition validation passed.'
