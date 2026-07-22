$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$logic=Get-Content (Join-Path $root 'src\RimForge.App\Features\ModInspector\MainWindow.SelectionNavigation.cs') -Raw
$forge=(Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml') -Raw)+"`n"+(Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml.cs') -Raw)
foreach($token in @('CanNavigateSelectionBack','CanNavigateSelectionForward','SelectModByPackageId')){if(-not $logic.Contains($token)){throw "Missing selection navigation implementation: $token"}}
if($forge -notmatch 'ModNavigationRequested|SourceId|TargetId'){throw 'ForgeView mod navigation contract is missing.'}
Write-Host 'Selection intelligence and navigation validation passed.'
