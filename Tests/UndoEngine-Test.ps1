$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$undoPath = Join-Path $root 'src\RimForge.App\Undo\UndoService.cs'
$registryPath = Join-Path $root 'src\RimForge.App\Commands\RimForgeCommandRegistry.cs'
$commandPath = Join-Path $root 'src\RimForge.App\Features\Navigation\MainWindow.Commands.cs'
$sorterPath = Join-Path $root 'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs'
$compositionPath = Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs'

foreach ($path in @($undoPath, $registryPath, $commandPath, $sorterPath, $compositionPath)) {
    if (-not (Test-Path $path)) { throw "Missing undo infrastructure file: $path" }
}

$undo = Get-Content $undoPath -Raw
foreach ($token in @('interface IUndoService', 'bool CanUndo', 'void Register', 'bool TryUndo', 'void Clear', 'private UndoEntry? _pending')) {
    if (-not $undo.Contains($token)) { throw "Missing single-level undo behavior: $token" }
}

$registry = Get-Content $registryPath -Raw
foreach ($token in @('Action Undo', 'Func<bool> CanUndo', 'RimForgeCommands.Undo, handlers.Undo, handlers.CanUndo')) {
    if (-not $registry.Contains($token)) { throw "Undo is not routed through the command framework: $token" }
}

$commands = Get-Content $commandPath -Raw
foreach ($token in @('ExecuteUndoCommand', '_undoService.TryUndo()', '_undoService.CanUndo', 'CommandManager.InvalidateRequerySuggested')) {
    if (-not $commands.Contains($token)) { throw "Missing Ctrl+Z command integration: $token" }
}

$sorter = Get-Content $sorterPath -Raw
foreach ($token in @('CaptureLoadOrderUndoSnapshot', 'RegisterLoadOrderUndo', 'RestoreLoadOrderUndoSnapshot', 'Auto-sort load order', 'Enable {item.DisplayName}', 'Disable {moving[0].DisplayName}')) {
    if (-not $sorter.Contains($token)) { throw "Missing undoable Mod Sorter operation: $token" }
}

$composition = Get-Content $compositionPath -Raw
foreach ($token in @('IUndoService', 'new UndoService()', 'public IUndoService UndoService')) {
    if (-not $composition.Contains($token)) { throw "Undo service is not composed centrally: $token" }
}

Write-Host 'Undo engine validation passed.'
