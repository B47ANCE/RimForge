$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$launch = Get-Content (Join-Path $root 'src/RimForge.App/Features/LaunchBar/MainWindow.LaunchBar.cs') -Raw
$ignoreStore = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/IssueIgnoreStore.cs') -Raw
$issueFlow = Get-Content (Join-Path $root 'src/RimForge.App/Features/IssueViewer/MainWindow.IssueViewer.cs') -Raw
$tooltip = Get-Content (Join-Path $root 'src/RimForge.UI/ViewModels/ProfileLoadOrderItemViewModel.cs') -Raw

if ($launch -notmatch '_analysisSnapshot is null' -or $launch -notmatch '_issueEngine\.Build') {
    throw 'Launch readiness must use the shared cached analysis snapshot.'
}
if ($launch -notmatch 'profile\.ActiveMods\.ToArray') {
    throw 'Launch readiness must be scoped to the profile being launched.'
}
if ($launch -match 'AnalyzeAsync|RefreshAnalysisSnapshot|RefreshLibrary|File\.ReadAllText') {
    throw 'Launch readiness must not trigger analysis or file reads.'
}
if ($ignoreStore -notmatch 'var next = new HashSet<string>' -or $ignoreStore -notmatch '_ignored\.UnionWith\(next\)') {
    throw 'Ignore state must update in memory only after persistence succeeds.'
}
if ($ignoreStore -notmatch 'finally' -or $ignoreStore -notmatch 'File\.Delete\(temporaryPath\)') {
    throw 'Ignore persistence must clean up temporary files.'
}
if ($issueFlow -notmatch 'RefreshAnalysisSnapshot\(\)' -or $issueFlow -notmatch 'RebuildProfileLoadOrder\(\)') {
    throw 'Repairs must refresh shared analysis and health projections.'
}
if ($tooltip -match 'AnalyzeAsync|File\.Read|Directory\.|EnumerateFiles|GetFiles') {
    throw 'Health tooltip path must remain cache-only.'
}
Write-Host 'Pass45A17Commit11FinalClosure-Test: PASSED' -ForegroundColor Green
