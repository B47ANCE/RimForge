$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$files=@('src\RimForge.Core\Models\DependencyAssistanceMode.cs','src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs','src\RimForge.App\Features\Settings\MainWindow.Settings.cs','src\RimForge.App\Features\Settings\SettingsView.xaml')
$text=($files|ForEach-Object{Get-Content (Join-Path $root $_) -Raw}) -join "`n"
foreach($token in @('DependencyAssistancePreference','DependencyAssistanceModes','PlanActivation','ResolveDependencyAssistance','Missing dependencies','DependencyAssistanceMode')){if(-not $text.Contains($token)){throw "Missing dependency assistance contract: $token"}}
Write-Host 'Automatic dependency assistance validation passed.'
