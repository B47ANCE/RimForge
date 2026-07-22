$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainXaml = Join-Path $root 'src\RimForge.App\MainWindow.xaml'
$mainCode = Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs'
$viewXaml = Join-Path $root 'src\RimForge.App\Features\IssueViewer\IssueViewerView.xaml'
$viewCode = Join-Path $root 'src\RimForge.App\Features\IssueViewer\IssueViewerView.xaml.cs'
$featureCode = Join-Path $root 'src\RimForge.App\Features\IssueViewer\MainWindow.IssueViewer.cs'

foreach ($path in @($mainXaml, $mainCode, $viewXaml, $viewCode, $featureCode)) {
    if (-not (Test-Path $path)) { throw "Missing required Issue Viewer decomposition file: $path" }
}

$mainMarkup = Get-Content $mainXaml -Raw
$mainBehavior = Get-Content $mainCode -Raw
$featureMarkup = Get-Content $viewXaml -Raw
$featureBehavior = Get-Content $featureCode -Raw

if ($mainMarkup -notmatch '<issueviewer:IssueViewerView') {
    throw 'MainWindow must host the IssueViewerView feature control.'
}

foreach ($forbidden in @('Text="ISSUE VIEWER"', 'Content="Fix All Issues"', 'Text="WHAT RIMFORGE FOUND"', 'x:Name="IssueViewerGrid"')) {
    if ($mainMarkup -match [regex]::Escape($forbidden)) {
        throw "Issue Viewer presentation drifted back into MainWindow.xaml: $forbidden"
    }
}

foreach ($required in @('ISSUE VIEWER', 'Fix All Issues', 'WHAT RIMFORGE FOUND', 'IssueList')) {
    if ($featureMarkup -notmatch [regex]::Escape($required)) {
        throw "IssueViewerView.xaml is missing expected presentation content: $required"
    }
}

foreach ($forbiddenMethod in @('private void RebuildIssueViewer()', 'private void FixSelectedIssue_Click(', 'private void FixAllIssues_Click(')) {
    if ($mainBehavior -match [regex]::Escape($forbiddenMethod)) {
        throw "Issue Viewer behavior drifted back into MainWindow.xaml.cs: $forbiddenMethod"
    }
}

foreach ($requiredMethod in @('private void RebuildIssueViewer()', 'IssueViewer_FixSelectedRequested', 'IssueViewer_FixAllRequested')) {
    if ($featureBehavior -notmatch [regex]::Escape($requiredMethod)) {
        throw "Issue Viewer feature coordination is missing: $requiredMethod"
    }
}

Write-Host 'Issue Viewer feature decomposition validation passed.' -ForegroundColor Green
