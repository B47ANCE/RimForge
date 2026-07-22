$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$interfaces = Get-Content (Join-Path $root 'src\RimForge.Core\Services\ApplicationContextInterfaces.cs') -Raw
$search = Get-Content (Join-Path $root 'src\RimForge.Core\Services\SearchContext.cs') -Raw
$navigation = Get-Content (Join-Path $root 'src\RimForge.Core\Services\NavigationContext.cs') -Raw
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
$window = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
$selectionNavigation = Get-Content (Join-Path $root 'src\RimForge.App\Features\ModInspector\MainWindow.SelectionNavigation.cs') -Raw

foreach ($token in @('interface ISearchContext', 'interface INavigationContext')) {
    if (-not $interfaces.Contains($token)) { throw "Missing shared context contract: $token" }
}
foreach ($token in @('sealed class SearchContext', 'QueryChanged', 'SetQuery')) {
    if (-not $search.Contains($token)) { throw "Search context is incomplete: $token" }
}
foreach ($token in @('sealed class NavigationContext', 'MaximumHistoryLength = 50', 'GoBack()', 'GoForward()', 'NavigationChanged')) {
    if (-not $navigation.Contains($token)) { throw "Navigation context is incomplete: $token" }
}
foreach ($token in @('ISearchContext SearchContext', 'INavigationContext NavigationContext', 'new SearchContext(eventBus)', 'new NavigationContext(eventBus)')) {
    if (-not $composition.Contains($token)) { throw "Application composition is missing shared context wiring: $token" }
}
foreach ($token in @('_searchContext = services.SearchContext', '_navigationContext = services.NavigationContext', '_eventBus.Subscribe<SearchQueryChangedEvent>', '_eventBus.Subscribe<NavigationChangedEvent>')) {
    if (-not $window.Contains($token)) { throw "MainWindow is missing context integration: $token" }
}
if ($window -match 'private\s+string\s+_searchText') { throw 'MainWindow still owns duplicate search state.' }
if ($selectionNavigation -match '_selectionHistory|_selectionHistoryIndex|_isNavigatingSelectionHistory') { throw 'MainWindow still owns duplicate navigation-history state.' }
foreach ($token in @('_navigationContext.Record', 'NavigateActiveCollection')) {
    if (-not $selectionNavigation.Contains($token)) { throw "Selection navigation does not use NavigationContext: $token" }
}

Write-Host 'Shared context infrastructure validation passed.'
