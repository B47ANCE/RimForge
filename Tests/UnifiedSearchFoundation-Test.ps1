$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$main = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
$search = Get-Content (Join-Path $root 'src\RimForge.App\Features\Search\MainWindow.Search.cs') -Raw
$sorter = Get-Content (Join-Path $root 'src\RimForge.App\Features\ModSorter\ModSorterView.xaml') -Raw
$filter = Get-Content (Join-Path $root 'src\RimForge.Core\Services\ModFilteringService.cs') -Raw
$parser = Get-Content (Join-Path $root 'src\RimForge.Core\Models\StructuredSearchQuery.cs') -Raw

$requiredMain = @('public ICollectionView ActiveProfileModsView','public ICollectionView InactiveInstalledModsView','IssueItemsView.Filter = FilterIssueItem','ActiveProfileModsView.Filter = FilterProfileLoadOrderItem','InactiveInstalledModsView.Filter = FilterProfileLoadOrderItem','RefreshSearchAwareViews()')
foreach ($token in $requiredMain) { if (-not $main.Contains($token)) { throw "Missing shared search foundation token: $token" } }

$requiredSearch = @('ModsView.Refresh()','ModSorterView.Refresh()','ActiveProfileModsView.Refresh()','InactiveInstalledModsView.Refresh()','IssueItemsView.Refresh()','DependencyEdgesView.Refresh()','FilterProfileLoadOrderItem','FilterIssueItem')
foreach ($token in $requiredSearch) { if (-not $search.Contains($token)) { throw "Missing search-aware refresh/filter token: $token" } }

if (-not $sorter.Contains('ItemsSource="{Binding InactiveInstalledModsView}"')) { throw 'Inactive Mod Sorter list is not bound to its search-aware view.' }
if (-not $sorter.Contains('ItemsSource="{Binding ActiveProfileModsView}"')) { throw 'Active Mod Sorter list is not bound to its search-aware view.' }

$requiredFilter = @('MatchesIdentity','MatchesSource','"badge"','"requires"','"required-by"')
foreach ($token in $requiredFilter) { if (-not $filter.Contains($token)) { throw "Missing hybrid search support: $token" } }
if (-not $parser.Contains('new SearchClause("identity", token, true)')) { throw 'Plain text is not constrained to identity search.' }
if ($filter.Contains('Contains(mod.Evidence.SearchText, token)')) { throw 'Legacy broad plain-text evidence search remains enabled.' }

Write-Host 'Unified search foundation validation passed.'
