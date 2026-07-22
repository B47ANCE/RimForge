$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
function Assert-Contains([string]$path, [string]$token, [string]$message) {
    $content = Get-Content -LiteralPath $path -Raw
    if (-not $content.Contains($token)) { throw "$message Inspected: $path" }
}
$sorterXaml = Join-Path $root 'src\RimForge.App\Features\ModSorter\ModSorterView.xaml'
$sorterCode = Join-Path $root 'src\RimForge.App\Features\ModSorter\ModSorterView.xaml.cs'
$mainXaml = Join-Path $root 'src\RimForge.App\MainWindow.xaml'
$mainSorter = Join-Path $root 'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs'
$issueXaml = Join-Path $root 'src\RimForge.App\Features\IssueViewer\IssueViewerView.xaml'
$issueCode = Join-Path $root 'src\RimForge.App\Features\IssueViewer\IssueViewerView.xaml.cs'
Assert-Contains $sorterXaml 'Click="HealthAnvil_Click"' 'Health anvils are not wired as navigation controls.'
Assert-Contains $sorterCode 'HealthNavigationRequested' 'ModSorterView does not publish health navigation requests.'
Assert-Contains $mainXaml 'HealthNavigationRequested="ModSorter_HealthNavigationRequested"' 'MainWindow does not subscribe to health navigation.'
Assert-Contains $mainSorter 'ModSorter_HealthNavigationRequested' 'MainWindow health navigation handler is missing.'
Assert-Contains $mainSorter 'SelectedIssueItem = issue;' 'Health navigation does not select the matching issue.'
Assert-Contains $mainSorter 'ShowPage(IssueViewerWorkspacePanel, "Issue Viewer")' 'Health navigation does not open Issue Viewer.'
Assert-Contains $mainSorter 'IssueViewerFeature.FocusIssue(issue);' 'Health navigation does not focus issue details.'
Assert-Contains $issueXaml 'IsExpanded="{Binding IsSelected, RelativeSource={RelativeSource AncestorType=ListBoxItem}, Mode=OneWay}"' 'Selected issue details are not automatically expanded.'
Assert-Contains $issueCode 'public void FocusIssue(IssueWorkItem issue)' 'IssueViewer focus API is missing.'
Assert-Contains $issueCode 'IssueList.ScrollIntoView(issue);' 'IssueViewer does not scroll the requested issue into view.'
Write-Host 'Pass 47B.3 health-anvil Issue Viewer navigation gate passed.'
