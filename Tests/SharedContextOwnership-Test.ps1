$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = Get-ChildItem (Join-Path $root 'src\RimForge.App') -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }
$compositionPath = (Resolve-Path (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs')).Path
$constructors = @('new SearchContext(', 'new NavigationContext(', 'new UndoService(', 'new BackgroundTaskService(', 'new RimForgeCommandRegistry(')
foreach ($constructor in $constructors) {
    $hits = @()
    foreach ($file in $sourceFiles) {
        $text = Get-Content $file.FullName -Raw
        if ($text.Contains($constructor)) { $hits += $file.FullName }
    }
    if ($hits.Count -ne 1 -or $hits[0] -ne $compositionPath) {
        throw "$constructor must appear only in the composition root. Found: $($hits -join ', ')"
    }
}
Write-Host 'Shared context ownership validation passed.'
