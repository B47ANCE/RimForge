$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$issue = Get-Content (Join-Path $repo 'src\RimForge.App\Features\IssueViewer\MainWindow.IssueViewer.cs') -Raw
$settings = Get-Content (Join-Path $repo 'src\RimForge.App\Features\Settings\MainWindow.Settings.cs') -Raw
if ($issue -notmatch 'private\s+IssueIgnoreStore\?\s+_issueIgnoreStore') { throw 'Missing IssueIgnoreStore backing field.' }
if ($settings -notmatch 'record\s+SortingPolicySettings') { throw 'Missing SortingPolicySettings record.' }
if ($settings -notmatch 'private\s+SortingPolicySettings\s+_sortingPolicySettings') { throw 'Missing sorting policy backing field.' }
Write-Host 'Pass46A2CompileDeclarations-Test: PASSED' -ForegroundColor Green
