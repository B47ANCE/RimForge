[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [switch]$SkipStartupSmoke
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$started = [DateTimeOffset]::UtcNow
$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param([string]$Name,[string]$Status,[TimeSpan]$Duration,[string]$Detail='')
    $results.Add([pscustomobject]@{
        Name = $Name
        Status = $Status
        DurationMs = [math]::Round($Duration.TotalMilliseconds)
        Detail = $Detail
    })
}

function Invoke-Step {
    param([string]$Name,[scriptblock]$Action)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        & $Action | Out-Host
        $code = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }
        if ($code -ne 0) { throw "$Name exited with code $code." }
        $watch.Stop()
        Add-Result $Name 'PASS' $watch.Elapsed
        return [bool]$true
    }
    catch {
        $watch.Stop()
        Add-Result $Name 'FAIL' $watch.Elapsed $_.Exception.Message
        Write-Host "FAILED: $Name" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        return [bool]$false
    }
}

$excluded = @(
    'Run-AllTests.ps1',
    'Run-Phase1SystemsCheck.ps1',
    'Run-FocusedErrorRecovery.ps1',
    'Run-FocusedErrorRecovery2.ps1',
    'Run-FocusedErrorRecovery3.ps1',
    'Run-RuntimeTests.ps1',
    'EvidenceLake-Result-Test.ps1',
    'PlayerLog-Evidence-Test.ps1',
    'BackgroundTaskLifecycle-Test.ps1'
)

$allPassed = $true
Push-Location $root
try {
    if (-not $NoBuild) {
        if (-not (Invoke-Step 'dotnet restore' { & dotnet restore .\RimForge.sln })) { $allPassed = $false }
        if (-not (Invoke-Step "dotnet build ($Configuration)" { & dotnet build .\RimForge.sln -c $Configuration --no-restore })) { $allPassed = $false }
    }

    $tests = Get-ChildItem .\tests\*.ps1 |
        Where-Object { $_.Name -notin $excluded } |
        Sort-Object Name

    foreach ($test in $tests) {
        Write-Host "`n=== $($test.Name) ===" -ForegroundColor Cyan
        $passed = Invoke-Step "Test: $($test.Name)" {
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $test.FullName
        }
        if (-not $passed) { $allPassed = $false }
    }

    if (-not $SkipStartupSmoke -and $allPassed) {
        $app = Join-Path $root "src\RimForge.App\bin\$Configuration\net10.0-windows\RimForge.exe"
        if (Test-Path $app) {
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $process = Start-Process -FilePath $app -ArgumentList '--logging' -PassThru
            try {
                Start-Sleep -Seconds 8
                if ($process.HasExited -and $process.ExitCode -ne 0) {
                    throw "RimForge exited during startup smoke validation with code $($process.ExitCode)."
                }
                Add-Result 'Startup smoke test' 'PASS' $watch.Elapsed
            }
            catch {
                Add-Result 'Startup smoke test' 'FAIL' $watch.Elapsed $_.Exception.Message
                $allPassed = $false
            }
            finally {
                if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
                $process.Dispose()
            }
        }
        else {
            Add-Result 'Startup smoke test' 'FAIL' ([TimeSpan]::Zero) "Executable not found: $app"
            $allPassed = $false
        }
    }
}
finally {
    Pop-Location
}

$finished = [DateTimeOffset]::UtcNow
$report = [pscustomobject]@{
    SchemaVersion = 1
    StartedUtc = $started
    FinishedUtc = $finished
    Configuration = $Configuration
    Passed = @($results | Where-Object Status -eq 'PASS').Count
    Failed = @($results | Where-Object Status -eq 'FAIL').Count
    Results = $results
}

$reportsRoot = Join-Path $root 'Reports'
New-Item -ItemType Directory -Force -Path $reportsRoot | Out-Null
$reportPath = Join-Path $reportsRoot 'AllTests.json'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host "`nTest summary" -ForegroundColor Cyan
$results | Format-Table Name, Status, DurationMs, Detail -AutoSize
Write-Host "Report: $reportPath"

if (-not $allPassed) {
    Write-Host "`nTest suite failed." -ForegroundColor Red
    exit 1
}

Write-Host "`nAll executed tests passed." -ForegroundColor Green
exit 0
