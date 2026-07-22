$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Assert-Contains([string]$Path, [string]$Pattern, [string]$Message) {
    $content = Get-Content -LiteralPath (Join-Path $root $Path) -Raw
    if ($content -notmatch $Pattern) { throw $Message }
}

Assert-Contains 'src/RimForge.App/Features/CommandBar/EngineeringCommandBarView.xaml' 'Back_Click' 'Command bar Back is not wired.'
Assert-Contains 'src/RimForge.App/Features/CommandBar/EngineeringCommandBarView.xaml' 'Reforge_Click' 'Command bar Reforge is not wired.'
Assert-Contains 'src/RimForge.Core/Services/GlobalNavigationService.cs' 'WorkstationNavigationSnapshot' 'Global navigation snapshots are missing.'
Assert-Contains 'src/RimForge.App/Features/Navigation/MainWindow.GlobalNavigation.cs' 'Ctrl\+Left|Key\.Left' 'Global navigation keyboard handling is missing.'
Assert-Contains 'src/RimForge.App/Features/Navigation/MainWindow.GlobalNavigation.cs' 'ExecuteReforgeAsync' 'Reforge implementation is missing.'
Assert-Contains 'src/RimForge.Core/Services/ApplicationStatusService.cs' 'ApplicationStatusKind' 'Central application status service is missing.'
Assert-Contains 'src/RimForge.App/Features/ModInspector/MainWindow.SelectionNavigation.cs' 'GetActiveSelectionCollection' 'Inspector collection navigation is missing.'
Assert-Contains 'src/RimForge.App/Features/ModInspector/ModInspectorView.xaml' 'Previous' 'Inspector Previous label is missing.'
Assert-Contains 'src/RimForge.App/Features/Settings/SettingsView.xaml' 'Header="General"' 'General Settings tab is missing.'
Assert-Contains 'src/RimForge.App/Features/Settings/SettingsView.xaml' 'Header="Appearance"' 'Appearance Settings tab is missing.'
Assert-Contains 'src/RimForge.App/Features/Settings/SettingsView.xaml' 'Header="Profiles"' 'Profiles Settings tab is missing.'
Assert-Contains 'src/RimForge.App/Features/Settings/SettingsView.xaml' 'Header="Automation"' 'Automation Settings tab is missing.'
Assert-Contains 'src/RimForge.App/Features/Settings/SettingsView.xaml' 'Header="Advanced"' 'Advanced Settings tab is missing.'
Assert-Contains 'src/RimForge.App/Features/CommandBar/EngineeringCommandBarView.xaml' 'DynamicResource (CommandBarBackgroundBrush|WorkstationChromeBackgroundBrush)' 'Command bar is not using semantic resources.'
Assert-Contains 'src/RimForge.App/App.xaml' 'Themes/WorkstationChrome.xaml' 'Workstation chrome semantic resources are not composed at application scope.'
Assert-Contains 'Directory.Build.props' '2\.2\.0-alpha\.(4[0-9]|[5-9][0-9]|[1-9][0-9]{2,})' 'Repository version predates Pass 40.'

Write-Host 'Pass 40 completion static acceptance checks passed.' -ForegroundColor Green
