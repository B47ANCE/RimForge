$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sorterPath = Join-Path $root 'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs'
$windowPath = Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs'
if (-not (Test-Path $sorterPath)) { throw 'Missing Mod Sorter implementation.' }
$sorter = Get-Content $sorterPath -Raw
foreach ($token in @(
    '_dependencyManagementService.PlanRemoval',
    'QueueDependencyRemovalConfirmation',
    'DisablePendingDependencyRemoval',
    'DisableModGroup',
    'QueueOrphanCleanupSuggestion',
    'OrphanCleanupPreference',
    'RemovePendingOrphans',
    'disable-impacted',
    'remove-orphans',
    'Unused dependencies detected',
    'RegisterLoadOrderUndo'
)) {
    if (-not $sorter.Contains($token)) { throw "Missing dependency-removal safety behavior: $token" }
}
$window = Get-Content $windowPath -Raw
foreach ($token in @('_pendingDependencyRemovalNotificationId','_pendingOrphanCleanupNotificationId','DisablePendingDependencyRemoval(applicationEvent.NotificationId)','RemovePendingOrphans(applicationEvent.NotificationId)')) {
    if (-not $window.Contains($token)) { throw "Missing dependency-removal action wiring: $token" }
}
Write-Host 'Dependency removal safety validation passed.' -ForegroundColor Green
