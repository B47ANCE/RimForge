$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$search=Get-Content (Join-Path $root 'src\RimForge.App\Features\Search\MainWindow.Search.cs') -Raw
foreach($token in @('NotifySearchResultState','ActiveProfileFilteredCount','IssueFilteredCount','ForgeSearchMatchCount','SearchSummaryText')){if(-not $search.Contains($token)){throw "Missing unified-search state: $token"}}
$views=@('src\RimForge.App\Features\ModSorter\ModSorterView.xaml','src\RimForge.App\Features\IssueViewer\IssueViewerView.xaml','src\RimForge.App\Features\ForgeView\ForgeViewView.xaml')
$text=($views|ForEach-Object{Get-Content (Join-Path $root $_) -Raw}) -join "`n"
if($text -notmatch 'IsSearchActive|SearchMatch|SearchSummary'){throw 'Search-aware feature emphasis is missing.'}
Write-Host 'Unified search presentation validation passed.'
