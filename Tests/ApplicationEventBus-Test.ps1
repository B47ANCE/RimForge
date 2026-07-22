$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$busPath = Join-Path $root 'src\RimForge.Core\Services\ApplicationEventBus.cs'
$eventsPath = Join-Path $root 'src\RimForge.Core\Services\ApplicationEvents.cs'
$compositionPath = Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs'
$mainWindowPath = Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs'
$searchPath = Join-Path $root 'src\RimForge.Core\Services\SearchContext.cs'
$selectionPath = Join-Path $root 'src\RimForge.Core\Services\SelectionService.cs'
$profilePath = Join-Path $root 'src\RimForge.Core\Services\ProfileWorkspaceStateService.cs'
$navigationPath = Join-Path $root 'src\RimForge.Core\Services\NavigationContext.cs'
$forgePath = Join-Path $root 'src\RimForge.Infrastructure\Services\ForgeSessionService.cs'
$backgroundPath = Join-Path $root 'src\RimForge.Infrastructure\Services\BackgroundTaskService.cs'

foreach ($path in @($busPath, $eventsPath, $compositionPath, $mainWindowPath, $searchPath, $selectionPath, $profilePath, $navigationPath, $forgePath, $backgroundPath)) {
    if (-not (Test-Path $path)) { throw "Missing event-bus file: $path" }
}

$busSource = Get-Content $busPath -Raw
foreach ($token in @('interface IApplicationEventBus', 'Subscribe<TEvent>', 'Publish<TEvent>', 'IDisposable', 'ApplicationEventBus')) {
    if (-not $busSource.Contains($token)) { throw "Missing event-bus contract token: $token" }
}

$eventSource = Get-Content $eventsPath -Raw
foreach ($token in @(
    'SearchQueryChangedEvent',
    'ModSelectionChangedEvent',
    'ProfileWorkspaceChangedEvent',
    'NavigationChangedEvent',
    'ForgeSessionChangedEvent',
    'BackgroundTaskChangedEvent',
    'ModLibraryChangedEvent',
    'IssueStateChangedEvent',
    'SettingsChangedEvent'
)) {
    if (-not $eventSource.Contains($token)) { throw "Missing typed application event: $token" }
}

$compositionSource = Get-Content $compositionPath -Raw
foreach ($token in @('var eventBus = new ApplicationEventBus()', 'public IApplicationEventBus EventBus', 'new SearchContext(eventBus)', 'new SelectionService(eventBus)', 'new ForgeSessionService(', 'new BackgroundTaskService(eventBus)')) {
    if (-not $compositionSource.Contains($token)) { throw "Missing event-bus composition token: $token" }
}

$publishers = @{
    $searchPath = 'SearchQueryChangedEvent'
    $selectionPath = 'ModSelectionChangedEvent'
    $profilePath = 'ProfileWorkspaceChangedEvent'
    $navigationPath = 'NavigationChangedEvent'
    $forgePath = 'ForgeSessionChangedEvent'
    $backgroundPath = 'BackgroundTaskChangedEvent'
}
foreach ($entry in $publishers.GetEnumerator()) {
    $source = Get-Content $entry.Key -Raw
    if (-not $source.Contains($entry.Value)) { throw "Publisher does not emit $($entry.Value): $($entry.Key)" }
}

$mainWindowSource = Get-Content $mainWindowPath -Raw
foreach ($token in @('Subscribe<ForgeSessionChangedEvent>', 'Subscribe<SearchQueryChangedEvent>', 'Subscribe<NavigationChangedEvent>', 'Subscribe<BackgroundTaskChangedEvent>', 'subscription.Dispose()')) {
    if (-not $mainWindowSource.Contains($token)) { throw "Missing shell event-bus subscription token: $token" }
}
foreach ($legacy in @('_forgeSessionService.SessionChanged +=', '_searchContext.QueryChanged +=', '_navigationContext.NavigationChanged +=', '_backgroundTaskService.TaskChanged +=')) {
    if ($mainWindowSource.Contains($legacy)) { throw "Legacy direct shell subscription remains: $legacy" }
}

Write-Host 'Typed application event bus validation passed.'
