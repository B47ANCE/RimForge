param(
    [switch]$SkipBuild,
    [switch]$SkipLaunch,
    [int]$LaunchObservationSeconds = 8
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$results = [System.Collections.Generic.List[object]]::new()
$started = Get-Date

function Invoke-CertificationStep {
    param([string]$Name, [scriptblock]$Action)
    $stepStart = Get-Date
    try {
        & $Action
        $results.Add([pscustomobject]@{ Name=$Name; Status='PASS'; Duration=((Get-Date)-$stepStart); Detail='' })
    }
    catch {
        $results.Add([pscustomobject]@{ Name=$Name; Status='FAIL'; Duration=((Get-Date)-$stepStart); Detail=$_.Exception.Message })
        throw
    }
}

$tests = @(
    'ApplicationComposition-Test.ps1',
    'ServiceLifetime-Test.ps1',
    'StartupLifecycle-Test.ps1',
    'ShutdownLifecycle-Test.ps1',
    'EventBusArchitecture-Test.ps1',
    'SharedContextOwnership-Test.ps1',
    'BackgroundTaskComposition-Test.ps1',
    'CommandRegistryArchitecture-Test.ps1',
    'RepositoryHygiene-Test.ps1',
    'ApplicationEventBus-Test.ps1',
    'BackgroundTaskFramework-Test.ps1',
    'SharedContextInfrastructure-Test.ps1',
    'UnifiedCommandFramework-Test.ps1',
    'UndoEngine-Test.ps1',
    'FirstRunProfileSafety-Test.ps1',
    'FeatureDecomposition-Test.ps1',
    'XamlResourceSmoke-Test.ps1',
    'StyleBasedOnRuntimeSafety-Test.ps1',
    'GlobalScrollbarResourceScope-Test.ps1',
    'UnifiedSearchCompletion-Test.ps1',
    'ChromePlusTopBarUnifiedSearch-Test.ps1',
    'StructuredSearchQuery-Test.ps1',
    'ForgeViewSelectionSync-Test.ps1',
    'SelectionIntelligenceNavigation-Test.ps1',
    'IssueDrivenNavigation-Test.ps1',
    'IssueViewerFeatureDecomposition-Test.ps1',
    'SettingsFeatureDecomposition-Test.ps1',
    'ForgeViewConsoleFeatureDecomposition-Test.ps1'
)

foreach ($test in $tests) {
    $path = Join-Path $PSScriptRoot $test
    if (-not (Test-Path $path)) { throw "Certification test is missing: $test" }
    Invoke-CertificationStep "Test: $test" { powershell -NoProfile -ExecutionPolicy Bypass -File $path }
}

if (-not $SkipBuild) {
    Invoke-CertificationStep 'dotnet clean' { dotnet clean (Join-Path $root 'RimForge.sln') }
    Invoke-CertificationStep 'dotnet build' { dotnet build (Join-Path $root 'RimForge.sln') }
}

if (-not $SkipLaunch) {
    $exe = Join-Path $root 'src\RimForge.App\bin\Debug\net10.0-windows\RimForge.exe'
    Invoke-CertificationStep 'Startup smoke test' {
        if (-not (Test-Path $exe)) { throw "Built executable was not found: $exe" }
        $eventStart = Get-Date
        $process = Start-Process -FilePath $exe -ArgumentList '--logging' -PassThru
        Start-Sleep -Seconds $LaunchObservationSeconds
        $process.Refresh()
        if ($process.HasExited) {
            $runtimeEvent = Get-WinEvent -FilterHashtable @{ LogName='Application'; StartTime=$eventStart } -ErrorAction SilentlyContinue |
                Where-Object { $_.ProviderName -eq '.NET Runtime' -and $_.Message -match 'RimForge' } |
                Select-Object -First 1 -ExpandProperty Message
            throw "RimForge exited during startup smoke test. $runtimeEvent"
        }
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}

$reportDirectory = Join-Path $root 'Reports'
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$reportPath = Join-Path $reportDirectory 'Phase1SystemsCheck.json'
[pscustomobject]@{
    StartedUtc = $started.ToUniversalTime().ToString('o')
    CompletedUtc = (Get-Date).ToUniversalTime().ToString('o')
    Machine = $env:COMPUTERNAME
    PowerShell = $PSVersionTable.PSVersion.ToString()
    Results = $results
} | ConvertTo-Json -Depth 6 | Set-Content $reportPath -Encoding utf8

$results | Format-Table Name, Status, Duration, Detail -AutoSize
Write-Host "Phase 1 automated systems check passed. Report: $reportPath"
