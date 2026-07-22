$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
function Assert-Contains([string]$path, [string]$token, [string]$message) {
    $content = Get-Content -LiteralPath $path -Raw
    if (-not $content.Contains($token)) { throw "$message Inspected: $path" }
}
$mainXaml = Join-Path $root 'src\RimForge.App\MainWindow.xaml'
$mainCode = Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs'
Assert-Contains $mainXaml 'Content="Resolve Issue"' 'Forge attention checkpoint is missing Resolve Issue.'
Assert-Contains $mainXaml 'Content="Ignore for Now"' 'Forge attention checkpoint is missing Ignore for Now.'
Assert-Contains $mainXaml 'Click="ResolveForgeAttention_Click"' 'Resolve Issue is not wired.'
Assert-Contains $mainXaml 'Click="IgnoreForgeAttention_Click"' 'Ignore for Now is not wired.'
Assert-Contains $mainXaml 'IsForgeAttentionRequired' 'Forge attention actions are not state-driven.'
Assert-Contains $mainCode 'GetNextForgeAttentionIssue' 'Forge attention queue selection is missing.'
Assert-Contains $mainCode 'IssueIgnoreStore.SetIgnored(issue.Id, true)' 'Ignore for Now does not use the persistent ignored-issue store.'
Assert-Contains $mainCode 'IssueViewerFeature.FocusIssue(issue);' 'Resolve Issue does not focus the actionable finding.'
Assert-Contains $mainCode 'ConsoleFeature.SelectActivityTab();' 'Technical failures do not fall back to Console.'
Assert-Contains $mainCode 'Ignored for now.' 'Attention queue does not advance after an ignored finding.'
Write-Host 'Pass 47B.4 resumable Forge attention checkpoint gate passed.' -ForegroundColor Green
