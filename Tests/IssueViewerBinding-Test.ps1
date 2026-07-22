$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$xaml = Get-Content (Join-Path $root 'src\RimForge.App\Features\IssueViewer\IssueViewerView.xaml') -Raw
$featureCode = Get-Content (Join-Path $root 'src\RimForge.App\Features\IssueViewer\MainWindow.IssueViewer.cs') -Raw
$viewCode = Get-Content (Join-Path $root 'src\RimForge.App\Features\IssueViewer\IssueViewerView.xaml.cs') -Raw
$models = Get-Content (Join-Path $root 'src\RimForge.Analysis\Models\IssueModels.cs') -Raw
$engine = Get-Content (Join-Path $root 'src\RimForge.Analysis\Services\IssueEngine.cs') -Raw

$required = @(
    'ItemsSource="{Binding IssueItemsView}"',
    'SelectedItem="{Binding SelectedIssueItem}"',
    'Click="FixSelectedIssue_Click"',
    'Click="FixAllIssues_Click"'
)
foreach ($token in $required) {
    if (-not $xaml.Contains($token)) { throw "Missing Issue Viewer XAML binding: $token" }
}
if (-not $featureCode.Contains('RebuildIssueViewer()')) { throw 'Issue Viewer rebuild path is missing.' }
if (-not $featureCode.Contains('IssueItems.ReplaceAll')) { throw 'Issue Viewer does not apply issues atomically.' }
if (-not $viewCode.Contains('FixSelectedRequestedEvent')) { throw 'Issue Viewer selected-repair routed event is missing.' }
if (-not $viewCode.Contains('FixAllRequestedEvent')) { throw 'Issue Viewer fix-all routed event is missing.' }
if (-not $models.Contains('IssueWorkItem')) { throw 'IssueWorkItem model is missing.' }
if (-not $engine.Contains('IssueViewerSnapshot Build')) { throw 'IssueEngine build contract is missing.' }
Write-Host 'Issue Viewer binding test passed.'
