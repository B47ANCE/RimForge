$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$tests=@(
"+",
".join("    '"+n+"'" for n in fail_names)+"
)
$failed=@()
foreach($test in $tests){Write-Host "`n=== $test ===" -ForegroundColor Cyan; & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $test); if($LASTEXITCODE -ne 0){$failed+=$test}}
if($failed.Count -gt 0){throw "Focused recovery failures: $($failed -join ', ')"}
Write-Host '`nFocused error recovery pass 2 completed.' -ForegroundColor Green
