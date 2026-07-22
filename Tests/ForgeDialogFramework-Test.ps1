$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$service = Join-Path $root 'src\RimForge.UI\Dialogs\ForgeDialogService.cs'
$window = Join-Path $root 'src\RimForge.UI\Dialogs\ForgeDialogWindow.xaml'
$app = Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs'
$icon = Join-Path $root 'src\RimForge.UI\Controls\ForgeIcon.cs'

foreach ($path in @($service, $window, $app, $icon)) {
    if (-not (Test-Path $path)) { throw "Missing required dialog file: $path" }
}

$serviceText = Get-Content $service -Raw
$windowText = Get-Content $window -Raw
$appText = Get-Content $app -Raw
$iconText = Get-Content $icon -Raw

if ($appText -match 'MessageBox\.Show') { throw 'Production MessageBox.Show call remains in MainWindow.xaml.cs.' }
if ($serviceText -notmatch 'ShowRepairPlanSummary') { throw 'Repair-plan summary dialog is not implemented.' }
if ($serviceText -notmatch 'ShowRepairPreview') { throw 'Individual repair preview dialog is not implemented.' }
if ($windowText -notmatch 'DetailContent') { throw 'ForgeDialogWindow does not expose rich detail content.' }
if ($windowText -notmatch 'WindowStyle="None"') { throw 'Custom dialog chrome is missing.' }
if ($iconText -notmatch 'ForgeIconKind\.Repair') { throw 'Repair workflow icon is missing.' }

[xml](Get-Content $window -Raw) | Out-Null
Write-Host 'Forge dialog framework test passed.'

$mainWindow = @((Get-Content -LiteralPath (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw), (Get-Content -LiteralPath (Join-Path $root 'src\RimForge.App\Features\IssueViewer\MainWindow.IssueViewer.cs') -Raw)) -join "`n"
if ($mainWindow -notmatch [regex]::Escape('IssueEngine.Build')) {
    throw 'Issue Viewer is not supplying the native mod library to IssueEngine.Build.'
}
