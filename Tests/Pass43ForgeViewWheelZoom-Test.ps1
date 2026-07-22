$ErrorActionPreference = 'Stop'

function Assert-FileContains([string]$Path, [string]$Pattern, [string]$Message) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing file: $Path" }
    $content = Get-Content -LiteralPath $Path -Raw
    if ($content -notmatch $Pattern) { throw $Message }
}

$root = Split-Path -Parent $PSScriptRoot
$view = Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml.cs'
$canvas = Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeGraphCanvas.cs'
$xaml = Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml'

Assert-FileContains $view 'GraphViewport\.AddHandler\(' 'ForgeView does not register wheel input programmatically.'
Assert-FileContains $view 'Mouse\.PreviewMouseWheelEvent' 'ForgeView does not register the preview wheel routed event.'
Assert-FileContains $view 'handledEventsToo:\s*true' 'ForgeView wheel input can still be suppressed by an ancestor ScrollViewer.'
Assert-FileContains $view 'GraphCanvas\.ZoomAt\(e\.Delta,\s*e\.GetPosition\(GraphCanvas\)\)' 'ForgeView does not forward wheel delta and pointer position into graph zoom.'
Assert-FileContains $view 'e\.Handled\s*=\s*true' 'ForgeView does not consume wheel input after zooming.'
Assert-FileContains $canvas 'public void ZoomAt\(int wheelDelta, Point anchor\)' 'ForgeGraphCanvas does not expose anchored wheel zoom.'
Assert-FileContains $canvas 'Math\.Clamp\(value,\s*\.35,\s*2\.5\)' 'ForgeGraphCanvas does not constrain wheel zoom.'

$xamlContent = Get-Content -LiteralPath $xaml -Raw
if ($xamlContent -match 'PreviewMouseWheel="GraphViewport_PreviewMouseWheel"') {
    throw 'ForgeView still has the duplicate XAML wheel handler.'
}

Write-Host 'Pass 43 ForgeView wheel zoom validation passed.' -ForegroundColor Green
