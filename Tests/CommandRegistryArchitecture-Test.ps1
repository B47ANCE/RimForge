$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$registryPath = Join-Path $root 'src\RimForge.App\Commands\RimForgeCommandRegistry.cs'
$commandsPath = Join-Path $root 'src\RimForge.App\Commands\RimForgeCommands.cs'
$mainPath = Join-Path $root 'src\RimForge.App\Features\Navigation\MainWindow.Commands.cs'
$registry = Get-Content $registryPath -Raw
$commands = Get-Content $commandsPath -Raw
$main = Get-Content $mainPath -Raw
foreach ($command in @('SelectAll','Save','Undo','Delete','Rename','Cancel')) {
    if (-not $commands.Contains($command)) { throw "Command catalog missing: $command" }
}
if (-not $main.Contains('ConfigureCommandFramework()')) { throw 'MainWindow does not configure the unified command framework.' }
foreach ($token in @('Bind(window, RimForgeCommands.SelectAll','Bind(window, RimForgeCommands.Save','Bind(window, RimForgeCommands.Undo','Bind(window, RimForgeCommands.Delete','Bind(window, RimForgeCommands.Rename','Bind(window, RimForgeCommands.Cancel')) {
    if (-not $registry.Contains($token)) { throw "Command registry is missing centralized binding: $token" }
}
Write-Host 'Command registry architecture validation passed.'
