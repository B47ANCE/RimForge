$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$tests = @(
  'FirstRunProfileSafety-Test.ps1',
  'NativeForgeConversion-Test.ps1',
  'Pass45A16Commit11ReadinessIgnorePersistence-Test.ps1',
  'UnifiedSearchCompletion-Test.ps1'
)
foreach ($test in $tests) {
  Write-Host "`n=== $test ===" -ForegroundColor Cyan
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $test)
  if ($LASTEXITCODE -ne 0) { throw "Focused recovery test failed: $test" }
}
Write-Host "`nFocused error recovery pass 3 passed." -ForegroundColor Green
