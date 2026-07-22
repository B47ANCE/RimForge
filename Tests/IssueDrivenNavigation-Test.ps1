$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$issueViewPath = Join-Path $root 'src\RimForge.App\Features\IssueViewer\IssueViewerView.xaml'
$issueCodePath = Join-Path $root 'src\RimForge.App\Features\IssueViewer\IssueViewerView.xaml.cs'
$issueHostPath = Join-Path $root 'src\RimForge.App\Features\IssueViewer\MainWindow.IssueViewer.cs'
$mainWindowPath = Join-Path $root 'src\RimForge.App\MainWindow.xaml'
$modelsPath = Join-Path $root 'src\RimForge.Analysis\Models\IssueModels.cs'
$philosophyPath = Join-Path $root 'ENGINEERING_PHILOSOPHY.md'

foreach ($path in @($issueViewPath, $issueCodePath, $issueHostPath, $mainWindowPath, $modelsPath, $philosophyPath)) {
    if (-not (Test-Path $path)) { throw "Missing required file: $path" }
}

$issueViewXaml = Get-Content $issueViewPath -Raw
$issueViewCode = Get-Content $issueCodePath -Raw
$issueHostCode = Get-Content $issueHostPath -Raw
$mainWindowXaml = Get-Content $mainWindowPath -Raw
$issueModels = Get-Content $modelsPath -Raw
$engineeringPhilosophy = Get-Content $philosophyPath -Raw

foreach ($token in @('Inspect Mod', 'Open in ForgeView', 'RelatedMods', 'InspectMod_Click', 'OpenInForgeView_Click', 'RelatedMod_Click')) {
    if (-not $issueViewXaml.Contains($token)) { throw "Missing Issue Viewer navigation token: $token" }
}

foreach ($token in @('IssueModNavigationRequestedEventArgs', 'ModNavigationRequested', 'RaiseModNavigation')) {
    if (-not $issueViewCode.Contains($token)) { throw "Missing Issue Viewer navigation API: $token" }
}

foreach ($token in @('IssueViewer_ModNavigationRequested', 'SelectModByPackageId', 'ScrollToWorkspaceSection(ForgeViewPanel, "ForgeView")')) {
    if (-not $issueHostCode.Contains($token)) { throw "Missing Issue Viewer host navigation wiring: $token" }
}

if (-not $mainWindowXaml.Contains('ModNavigationRequested="IssueViewer_ModNavigationRequested"')) {
    throw 'Issue Viewer navigation event is not connected in MainWindow.xaml.'
}

foreach ($token in @('IssueRelatedMod', 'public IReadOnlyList<IssueRelatedMod> RelatedMods')) {
    if (-not $issueModels.Contains($token)) { throw "Missing related-mod presentation model: $token" }
}

foreach ($token in @('Build on reality', 'Validate before packaging', 'Runtime is the final gate')) {
    if (-not $engineeringPhilosophy.Contains($token)) { throw "Missing engineering philosophy rule: $token" }
}

Write-Host 'Issue-driven navigation validation passed.'
