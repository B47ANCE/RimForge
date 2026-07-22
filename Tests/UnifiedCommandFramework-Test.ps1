$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$catalogPath = Join-Path $root 'src\RimForge.App\Commands\RimForgeCommands.cs'
$registryPath = Join-Path $root 'src\RimForge.App\Commands\RimForgeCommandRegistry.cs'
$windowCommandsPath = Join-Path $root 'src\RimForge.App\Features\Navigation\MainWindow.Commands.cs'
$compositionPath = Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs'
$sorterPath = Join-Path $root 'src\RimForge.App\Features\ModSorter\ModSorterView.xaml'

foreach ($path in @($catalogPath, $registryPath, $windowCommandsPath, $compositionPath, $sorterPath)) {
    if (-not (Test-Path $path)) { throw "Missing command framework file: $path" }
}

$catalog = Get-Content $catalogPath -Raw
foreach ($token in @('SelectAll', 'Save', 'Undo', 'Delete', 'Rename', 'Cancel', 'Key.A', 'Key.S', 'Key.Z', 'Key.Delete', 'Key.F2', 'Key.Escape')) {
    if (-not $catalog.Contains($token)) { throw "Missing command catalog token: $token" }
}

$registry = Get-Content $registryPath -Raw
foreach ($token in @('IRimForgeCommandRegistry', 'CommandBinding', 'InputBindings.Add', 'RimForgeCommands.Undo', 'handlers.CanUndo')) {
    if (-not $registry.Contains($token)) { throw "Missing command registry token: $token" }
}

$windowCommands = Get-Content $windowCommandsPath -Raw
foreach ($token in @('ConfigureCommandFramework', 'SelectAllInFocusedModList', 'SaveLoadOrder_Click', 'DeleteProfile_Click', 'RenameProfile_Click', 'ExecuteCancelCommand')) {
    if (-not $windowCommands.Contains($token)) { throw "Missing MainWindow command integration: $token" }
}

$composition = Get-Content $compositionPath -Raw
foreach ($token in @('IRimForgeCommandRegistry', 'RimForgeCommandRegistry', 'CommandRegistry')) {
    if (-not $composition.Contains($token)) { throw "Missing command composition token: $token" }
}

$sorter = Get-Content $sorterPath -Raw
if (($sorter | Select-String -Pattern 'SelectionMode="Extended"' -AllMatches).Matches.Count -lt 2) {
    throw 'Both Mod Sorter lists must support extended selection.'
}

Write-Host 'Unified command framework validation passed.'
