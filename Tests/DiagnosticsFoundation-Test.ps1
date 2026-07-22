$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content (Join-Path $root 'src\RimForge.Core\Diagnostics\DiagnosticModels.cs') -Raw
$service = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\DiagnosticService.cs') -Raw
$session = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ForgeSessionService.cs') -Raw
$companion = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\CompanionHostService.cs') -Raw
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
$window = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
$xaml = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml') -Raw

$checks = @(
    @{ Name = 'Shared health model'; Pass = $models -match 'record RuntimeHealth' -and $models -match 'enum HealthStatus' },
    @{ Name = 'Diagnostic event'; Pass = $models -match 'record DiagnosticEvent' },
    @{ Name = 'Performance timer'; Pass = $models -match 'class PerformanceTimer' },
    @{ Name = 'Log sink contract'; Pass = $models -match 'interface ILogSink' },
    @{ Name = 'Session log contract'; Pass = $models -match 'interface ISessionLog' },
    @{ Name = 'Durable JSONL sink'; Pass = $service -match 'class JsonlLogSink' -and $service -match 'JsonSerializer\.Serialize' },
    @{ Name = 'Legacy logger bridge'; Pass = $service -match 'RimForgeLogger\.EntryWritten \+=' },
    @{ Name = 'Forge Session diagnostics'; Pass = $session -match '_diagnostics\?\.Write' -and $session -match '_sessionLog\?\.BeginSession' },
    @{ Name = 'Companion health diagnostics'; Pass = $companion -match '_diagnostics\?\.ReportHealth' },
    @{ Name = 'Canonical composition'; Pass = $composition -match 'new DiagnosticService' -and $composition -match 'rimforge-diagnostics\.jsonl' },
    @{ Name = 'Main-client health projection'; Pass = $window -match 'RuntimeHealthText' -and $xaml -match 'Text="{Binding RuntimeHealthText}"' }
)

$failed = $false
foreach ($check in $checks) {
    if ($check.Pass) { Write-Host "[PASS] $($check.Name)" -ForegroundColor Green }
    else { Write-Host "[FAIL] $($check.Name)" -ForegroundColor Red; $failed = $true }
}
if ($failed) { exit 1 }
Write-Host 'Shared diagnostics foundation validation passed.' -ForegroundColor Green
