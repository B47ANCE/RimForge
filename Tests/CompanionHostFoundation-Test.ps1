$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$hostRoot = Join-Path $root 'src\RimForge.Companion.Host'
$controller = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\CompanionHostService.cs') -Raw
$contracts = Get-Content (Join-Path $root 'src\RimForge.Core\Services\RuntimeInterfaces.cs') -Raw
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
$window = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
$xaml = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml') -Raw

$required = @('CompanionHost.cs','CompanionHostOptions.cs','CompanionHostHealth.cs','IpcServer.cs','PlayerLogWatcher.cs','RuntimeProcessMonitor.cs','SessionBridge.cs')
$checks = @(
    @{ Name = 'Host components'; Pass = -not @($required | Where-Object { -not (Test-Path (Join-Path $hostRoot $_)) }) },
    @{ Name = 'Companion controller contract'; Pass = $contracts -match 'interface ICompanionHost' },
    @{ Name = 'Hidden background process'; Pass = $controller -match 'CreateNoWindow = true' -and $controller -match 'ProcessWindowStyle\.Hidden' },
    @{ Name = 'Session-bound launch'; Pass = $controller -match '--session' -and $controller -match 'ForgeSessionId' },
    @{ Name = 'Application composition'; Pass = $composition -match 'new CompanionHostService\(diagnosticService\)' },
    @{ Name = 'Main-client state projection'; Pass = $window -match 'CompanionHostStatusText' -and $window -match 'CompanionHost_StateChanged' },
    @{ Name = 'Main-client UI'; Pass = $xaml -match 'Text="{Binding CompanionHostStatusText}"' }
)

$failed = $false
foreach ($check in $checks) {
    if ($check.Pass) { Write-Host "[PASS] $($check.Name)" -ForegroundColor Green }
    else { Write-Host "[FAIL] $($check.Name)" -ForegroundColor Red; $failed = $true }
}
if ($failed) { exit 1 }
Write-Host 'Companion Host foundation validation passed.' -ForegroundColor Green
