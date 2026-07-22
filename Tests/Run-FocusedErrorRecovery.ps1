$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$tests = @(
    'AutomaticDependencyAssistance-Test.ps1',
    'ChromePlusTopBarUnifiedSearch-Test.ps1',
    'DocumentationContract-Test.ps1',
    'FirstRunProfileSafety-Test.ps1',
    'ForgeDialogFramework-Test.ps1',
    'ForgeViewSuite-Test.ps1',
    'IssueViewerFeatureDecomposition-Test.ps1',
    'LaunchReadinessReview-Test.ps1',
    'NativeForgeConversion-Test.ps1',
    'RepositoryHygiene-Test.ps1',
    'RuntimeStartupReplay-Test.ps1',
    'RuntimeStorageIsolation-Test.ps1',
    'SelectionIntelligenceNavigation-Test.ps1',
    'SharedContextInfrastructure-Test.ps1',
    'SortTransactionCompileContract-Test.ps1',
    'SortTransactionRollback-Test.ps1',
    'StructuredSearchQuery-Test.ps1',
    'TriHybridSortingPipeline-Test.ps1',
    'UnifiedSearchCompletion-Test.ps1',
    'UnifiedSearchPresentation-Test.ps1'
)

$failures = @()
foreach ($test in $tests) {
    Write-Host "`n=== $test ===" -ForegroundColor Cyan
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $test)
    if ($LASTEXITCODE -ne 0) { $failures += $test }
}

if ($failures.Count -gt 0) {
    throw "Focused recovery failures: $($failures -join ', ')"
}

Write-Host "`nFocused error-recovery suite passed." -ForegroundColor Green
