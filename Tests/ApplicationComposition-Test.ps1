$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compositionPath = Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs'
if (-not (Test-Path $compositionPath)) { throw "Missing composition root: $compositionPath" }
$text = Get-Content $compositionPath -Raw
$required = @(
    'public sealed class RimForgeApplicationServices : IAsyncDisposable',
    'var eventBus = new ApplicationEventBus()',
    'var lifecycleService = new ApplicationLifecycleService(eventBus)',
    'new SearchContext(eventBus)',
    'new NavigationContext(eventBus)',
    'new BackgroundTaskService(eventBus)',
    'new ForgeSessionService(',
    'public async ValueTask DisposeAsync()'
)
foreach ($token in $required) {
    if (-not $text.Contains($token)) { throw "Composition root is missing: $token" }
}
foreach ($typeName in @('ApplicationEventBus','ApplicationLifecycleService','SearchContext','NavigationContext','UndoService','BackgroundTaskService','RimForgeCommandRegistry','ForgeSessionService')) {
    $count = ([regex]::Matches($text, "new\s+$typeName\s*\(")).Count
    if ($count -ne 1) { throw "$typeName must be composed exactly once; found $count." }
}
Write-Host 'Application composition validation passed.'
