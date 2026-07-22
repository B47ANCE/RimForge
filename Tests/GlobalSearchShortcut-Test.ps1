$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

function Assert-Contains([string]$Path, [string]$Pattern, [string]$Message) {
    if (-not (Test-Path $Path)) { throw "Missing file: $Path" }
    $content = Get-Content $Path -Raw
    if ($content -notmatch $Pattern) { throw $Message }
}

$windowXaml = Join-Path $repo 'src\RimForge.App\MainWindow.xaml'
$navigation = Join-Path $repo 'src\RimForge.App\Features\Navigation\MainWindow.GlobalNavigation.cs'
$commandBarCode = Join-Path $repo 'src\RimForge.App\Features\CommandBar\EngineeringCommandBarView.xaml.cs'
$commandBarXaml = Join-Path $repo 'src\RimForge.App\Features\CommandBar\EngineeringCommandBarView.xaml'

Assert-Contains $windowXaml 'x:Name="EngineeringCommandBar"' 'The command bar must be addressable by the shell.'
Assert-Contains $windowXaml 'PreviewKeyDown="MainWindow_PreviewKeyDown"' 'The shell-wide keyboard handler is missing.'
Assert-Contains $navigation 'e\.Key\s*==\s*Key\.F' 'Ctrl+F is not handled by the shell.'
Assert-Contains $navigation 'EngineeringCommandBar\.FocusGlobalSearch\(\)' 'Ctrl+F does not invoke the global search focus contract.'
Assert-Contains $navigation 'e\.Handled\s*=\s*true' 'The Ctrl+F shortcut must be marked handled.'
Assert-Contains $commandBarCode 'public void FocusGlobalSearch\(\)' 'The command bar does not expose a global search focus contract.'
Assert-Contains $commandBarCode 'GlobalSearchBox\.Focus\(\)' 'The search focus contract does not focus the search box.'
Assert-Contains $commandBarCode 'Keyboard\.Focus\(GlobalSearchBox\)' 'Keyboard focus is not explicitly assigned to the search box.'
Assert-Contains $commandBarCode 'GlobalSearchBox\.SelectAll' 'Existing search text is not selected for immediate replacement.'
Assert-Contains $commandBarXaml 'Text="Ctrl\+F"' 'The visible Ctrl+F affordance is missing.'

Write-Host 'Global Ctrl+F search shortcut validation passed.'
