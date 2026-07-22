$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
$main = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
if (-not $main.Contains('private readonly RimForgeApplicationServices _applicationServices;')) { throw 'MainWindow does not retain its composition-root owner.' }
if (-not $main.Contains('_applicationServices = services;')) { throw 'MainWindow does not retain the composed service graph.' }
if (-not $main.Contains('_applicationServices.DisposeAsync().AsTask()')) { throw 'MainWindow does not dispose the composition root.' }
if (-not $composition.Contains('Interlocked.Exchange(ref _disposeState, 1)')) { throw 'Composition disposal is not idempotent.' }
$forbidden = @('new SearchContext(', 'new NavigationContext(', 'new BackgroundTaskService(', 'new ForgeSessionService(', 'new ApplicationEventBus(')
foreach ($token in $forbidden) {
    if ($main.Contains($token)) { throw "MainWindow directly composes an application-scoped service: $token" }
}
Write-Host 'Service lifetime validation passed.'
