$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$viewCode = Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml.cs') -Raw
$mainCode = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
$canvasCode = Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeGraphCanvas.cs') -Raw
$queryCode = Get-Content (Join-Path $root 'src\RimForge.Core\Services\ForgeGraphQueryService.cs') -Raw

function Assert-Match([string]$content, [string]$pattern, [string]$message) {
    if ($content -notmatch $pattern) { throw $message }
}
function Assert-NotMatch([string]$content, [string]$pattern, [string]$message) {
    if ($content -match $pattern) { throw $message }
}

Assert-Match $viewCode 'OwnsGraphWheelInput\s*\(' 'ForgeView does not expose graph wheel ownership.'
Assert-Match $mainCode 'ForgeViewFeature\?\.OwnsGraphWheelInput' 'Main workspace scrolling does not yield to ForgeView graph wheel input.'
Assert-Match $mainCode 'OwnsGraphWheelInput[\s\S]*?e\.Handled\s*=\s*true;[\s\S]*?return;' 'Main workspace wheel routing does not stop page scrolling over ForgeView.'
Assert-Match $viewCode 'handledEventsToo:\s*true' 'ForgeView wheel zoom does not receive already-handled routed input.'
Assert-NotMatch $viewCode 'DependencyNodes\.ToDictionary' 'Outline still uses duplicate-unsafe direct dictionary construction.'
Assert-Match $queryCode 'GroupBy\(NodeId, StringComparer\.OrdinalIgnoreCase\)' 'Canonical graph query does not tolerate duplicate graph package identifiers.'
Assert-Match $queryCode 'nodes\.ContainsKey\(edge\.SourceId\).*nodes\.ContainsKey\(edge\.TargetId\)' 'Canonical graph query does not filter malformed or dangling relationships.'
Assert-Match $viewCode 'QueryService\.Execute' 'Outline bypasses canonical graph topology validation.'

Assert-Match $canvasCode 'InvalidateLayoutCache[\s\S]*?_hasInitializedView\s*=\s*false' 'Topology changes do not reset ForgeView for its default fit.'
Assert-Match $canvasCode 'if \(_logicalBounds\.IsEmpty[\s\S]*?_hasInitializedView\s*=\s*false;[\s\S]*?return;' 'An empty early layout incorrectly completes default view initialization.'
Assert-Match $canvasCode 'if \(canvas\._hasInitializedView\) canvas\.CenterOnPackage\(packageId\);[\s\S]*?else canvas\.FitToView\(\);' 'Initial graph selection can override the default full-graph fit.'

Write-Host 'Pass 43 ForgeView runtime interaction checks passed.' -ForegroundColor Green
