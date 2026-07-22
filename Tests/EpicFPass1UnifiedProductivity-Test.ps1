$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$catalogPath = Join-Path $root 'src\RimForge.App\Commands\ProductivityActionCatalog.cs'
$searchPath = Join-Path $root 'src\RimForge.App\Features\Search\MainWindow.Search.cs'
$sorterPath = Join-Path $root 'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs'
$sorterViewPath = Join-Path $root 'src\RimForge.App\Features\ModSorter\ModSorterView.xaml'

foreach ($path in @($catalogPath, $searchPath, $sorterPath, $sorterViewPath)) {
    if (-not (Test-Path $path)) { throw "Missing Epic F Pass 1 file: $path" }
}

$catalog = Get-Content $catalogPath -Raw
foreach ($token in @('ProductivityActionCatalog', 'navigate.mod-sorter', 'navigate.settings', 'profile.enable-selected', 'profile.disable-selected')) {
    if (-not $catalog.Contains($token)) { throw "Missing canonical productivity action: $token" }
}

$search = Get-Content $searchPath -Raw
foreach ($token in @('ProductivityActionCatalog.All', 'SearchDiscoveryKind.Command', 'ExecuteProductivityAction')) {
    if (-not $search.Contains($token)) { throw "Search does not use canonical productivity state: $token" }
}
if ($search.Contains('SearchableWorkspaces')) { throw 'Legacy duplicate workspace search catalog remains.' }

$sorter = Get-Content $sorterPath -Raw
foreach ($token in @('ExecuteBulkEnableSelected', 'ExecuteBulkDisableSelected', 'BuildBulkOperationPreview', 'ShowConfirmation', 'CaptureLoadOrderUndoSnapshot', 'RegisterLoadOrderUndo', 'RestoreLoadOrderUndoSnapshot')) {
    if (-not $sorter.Contains($token)) { throw "Bulk preview/undo contract is incomplete: $token" }
}

$sorterView = Get-Content $sorterViewPath -Raw
foreach ($token in @('Enable selected', 'Disable selected', 'EnableSelected_Click', 'DisableSelected_Click')) {
    if (-not $sorterView.Contains($token)) { throw "Bulk workflow is not exposed in Mod Sorter: $token" }
}

Write-Host 'Epic F Pass 1 unified productivity validation passed.'
