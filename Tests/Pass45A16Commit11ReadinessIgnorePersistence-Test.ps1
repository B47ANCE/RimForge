$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$launch = Get-Content (Join-Path $root 'src/RimForge.App/Features/LaunchBar/MainWindow.LaunchBar.cs') -Raw
$ignoreStore = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/IssueIgnoreStore.cs') -Raw
$issueFlow = Get-Content (Join-Path $root 'src/RimForge.App/Features/IssueViewer/MainWindow.IssueViewer.cs') -Raw
$completion = Get-Content (Join-Path $root 'Tests/Commit11Completion-Test.ps1') -Raw
if ($launch -notmatch '_issueEngine\.Build' -or $launch -notmatch 'IssueScopeKind\.ActiveProfile' -or $launch -notmatch 'IssueIgnoreStore\.Snapshot') { throw 'Launch readiness must build an ignored-aware target-profile projection.' }
if ($launch -notmatch 'profile\.ActiveMods\.ToArray') { throw 'Launch readiness must scope the projection to the profile being launched.' }
if ($launch -match 'AnalyzeAsync|RefreshAnalysisSnapshot|RefreshLibrary|File\.ReadAllText') { throw 'Launch readiness must remain advisory and cache-only.' }
if ($ignoreStore -notmatch '\.tmp' -or $ignoreStore -notmatch 'File\.Move\(.+overwrite:\s*true') { throw 'Ignore persistence must use atomic replacement.' }
if ($issueFlow -notmatch 'RebuildProfileLoadOrder\(\)') { throw 'Ignore state must refresh health-anvil projections.' }
if ($completion -notmatch 'current-architecture compatibility gate passed') {
    throw 'Commit 11 completion gate is not aligned with the current decomposed architecture.'
}
foreach ($gate in @('Pass45A14LoadOrderSortFreezeHotfix-Test.ps1','Pass45A15Commit11EndToEndIssueRepair-Test.ps1')) {
    if (-not (Test-Path (Join-Path $root ('Tests/' + $gate)))) { throw "Required historical regression test is missing: $gate" }
}
Write-Host 'Pass45A16Commit11ReadinessIgnorePersistence-Test: PASSED' -ForegroundColor Green
