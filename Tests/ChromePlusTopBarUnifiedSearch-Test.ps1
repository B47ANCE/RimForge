$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$bar=Get-Content (Join-Path $root 'src\RimForge.App\Features\CommandBar\EngineeringCommandBarView.xaml') -Raw
$code=Get-Content (Join-Path $root 'src\RimForge.App\Features\CommandBar\EngineeringCommandBarView.xaml.cs') -Raw
$search=Get-Content (Join-Path $root 'src\RimForge.App\Features\Search\MainWindow.Search.cs') -Raw
foreach($token in @('NavigationMenuButton','NavigationMenuPopup',"We're not just managing your mods",'SearchHost','GlobalSearchBox','SearchClearButton')){if(-not $bar.Contains($token)){throw "Chrome+ contract missing: $token"}}
foreach($token in @('GlobalSearchBox.Clear()','UpdateSource()')){if(-not $code.Contains($token)){throw "Command bar behavior missing: $token"}}
foreach($token in @('ActiveProfileModsView.Refresh()','IssueItemsView.Refresh()','DependencyEdgesView.Refresh()')){if(-not $search.Contains($token)){throw "Shared filtering missing: $token"}}
Write-Host 'Chrome+ top-bar unified search validation passed.'
