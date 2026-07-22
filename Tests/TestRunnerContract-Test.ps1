$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$runnerPath = Join-Path $PSScriptRoot 'Run-AllTests.ps1'
if (-not (Test-Path $runnerPath -PathType Leaf)) { throw 'Canonical all-tests runner is missing.' }
$text = Get-Content $runnerPath -Raw
foreach ($token in @(
    "'Run-AllTests.ps1'",
    "'Run-Phase1SystemsCheck.ps1'",
    'if (-not $allPassed)',
    'exit 1',
    'AllTests.json',
    'powershell.exe -NoProfile -ExecutionPolicy Bypass',
    '& $Action | Out-Host'
)) {
    if (-not $text.Contains($token)) { throw "Canonical test runner is missing contract token: $token" }
}
if ($text.IndexOf('if (-not $allPassed)') -gt $text.IndexOf('All executed tests passed.')) { throw 'Failure guard must precede success output.' }
if ($text.IndexOf('exit 1') -gt $text.IndexOf('All executed tests passed.')) { throw 'Failure exit must precede success output.' }
if ($text.IndexOf('exit 0') -lt $text.IndexOf('All executed tests passed.')) { throw 'Successful exit must follow success output.' }
Write-Host 'Canonical test runner contract passed.'
