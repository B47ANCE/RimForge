$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$issueEngine = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/IssueEngine.cs') -Raw
$issueView = Get-Content (Join-Path $root 'src/RimForge.App/Features/IssueViewer/IssueViewerView.xaml') -Raw
$issueFlow = Get-Content (Join-Path $root 'src/RimForge.App/Features/IssueViewer/MainWindow.IssueViewer.cs') -Raw
$tooltip = Get-Content (Join-Path $root 'src/RimForge.UI/ViewModels/ProfileLoadOrderItemViewModel.cs') -Raw
if ($issueEngine -notmatch 'ignoredIssueIds' -or $issueEngine -notmatch 'false,\s*"Missing Mods"') { throw 'Issue projection contract incomplete.' }
if ($issueView -notmatch 'Fix Issue' -or $issueView -notmatch 'ToggleIgnore_Click') { throw 'Issue Viewer actions incomplete.' }
if ($issueFlow -notmatch 'ExecuteAutomaticRepairAsync' -or $issueFlow -notmatch 'RefreshAnalysisSnapshot') { throw 'Repair execution/refresh incomplete.' }
if ($tooltip -match 'AnalyzeAsync|\bFile\.') { throw 'Tooltip path must remain cache-only.' }
Write-Host 'Commit 11 end-to-end issue repair contract present.'
