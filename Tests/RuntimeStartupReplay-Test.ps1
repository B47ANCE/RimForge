$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content (Join-Path $root 'src/RimForge.Core/Models/RuntimeModels.cs') -Raw
$interfaces = Get-Content (Join-Path $root 'src/RimForge.Core/Services/RuntimeInterfaces.cs') -Raw
$service = Get-Content (Join-Path $root 'src/RimForge.Infrastructure/Services/GameLogService.cs') -Raw
$window = Get-Content (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs') -Raw

$checks = [ordered]@{
    'Replay summary model exists' = $models -match 'record GameLogReplaySummary'
    'Replay reports unterminated final line' = $models -match 'IncludedUnterminatedFinalLine'
    'Runtime interface exposes replay completion' = $interfaces -match 'StartupReplayCompleted'
    'Service publishes replay summary' = $service -match 'StartupReplayCompleted\?\.Invoke'
    'Service includes final non-newline line' = $service -match 'ParseWindowLines' -and $service -match 'final.*line may therefore be valid'
    'Replay remains bounded' = $service -match 'InitialTailLines = 500' -and $service -match 'InitialTailBytes = 256 \* 1024'
    'UI subscribes to replay completion' = $window -match 'StartupReplayCompleted \+=' 
    'UI unsubscribes during shutdown' = $window -match 'StartupReplayCompleted -='
    'UI surfaces replay telemetry' = $window -match 'Replayed \{summary\.ReplayedEntries\} Player\.log startup entries'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
$checks.GetEnumerator() | ForEach-Object {
    $status = if ($_.Value) { 'PASS' } else { 'FAIL' }
    Write-Host "[$status] $($_.Key)"
}

if ($failed.Count -gt 0) {
    throw "Runtime startup replay contract failed: $($failed.Key -join ', ')"
}
