$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$xaml = Get-Content (Join-Path $root 'src\RimForge.App\Features\IssueViewer\IssueViewerView.xaml') -Raw
$model = Get-Content (Join-Path $root 'src\RimForge.Analysis\Models\IssueModels.cs') -Raw
$engine = Get-Content (Join-Path $root 'src\RimForge.Analysis\Services\IssueEngine.cs') -Raw

foreach ($required in @('Preview Repair','Fix Issue','Technical details','WHAT RIMFORGE FOUND','RECOMMENDED NEXT STEP')) {
    if ($xaml -notmatch [regex]::Escape($required)) { throw "Missing Issue Viewer UI element: $required" }
}
foreach ($required in @('ModName','Category','RelatedModNames','ResolutionLabel')) {
    if ($model -notmatch $required) { throw "Missing IssueWorkItem presentation field: $required" }
}
if ($engine -notmatch 'Choose which mod should load first') { throw 'Dependency-cycle resolution guidance is missing.' }
if ($xaml -match 'Header="MOD / PACKAGE ID"') { throw 'Package IDs remain exposed as a primary Issue Viewer column.' }
Write-Host 'Interactive Issue Viewer test passed.'
