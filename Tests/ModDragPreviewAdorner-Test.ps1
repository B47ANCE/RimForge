$ErrorActionPreference = 'Stop'

$path = Join-Path $PSScriptRoot '..\src\RimForge.App\Features\ModSorter\ModSorterDragAdorners.cs'
if (-not (Test-Path $path)) { throw "Missing ModSorter drag adorner source: $path" }

$content = Get-Content $path -Raw
$visualInit = $content.IndexOf('_visuals = new VisualCollection(this)', [System.StringComparison]::Ordinal)
$hitTestChange = $content.IndexOf('IsHitTestVisible = false', [System.StringComparison]::OrdinalIgnoreCase)

if ($visualInit -lt 0) { throw 'ModDragPreviewAdorner does not initialize its VisualCollection.' }
if ($hitTestChange -lt 0) { throw 'ModDragPreviewAdorner does not disable hit testing.' }
if ($visualInit -gt $hitTestChange) {
    throw 'VisualCollection must be initialized before changing IsHitTestVisible; WPF can query VisualChildrenCount during the property change.'
}
if ($content -notmatch 'VisualChildrenCount\s*=>\s*_visuals\.Count') {
    throw 'ModDragPreviewAdorner VisualChildrenCount is not backed by the initialized VisualCollection.'
}

Write-Host 'Mod drag preview adorner validation passed.' -ForegroundColor Green
