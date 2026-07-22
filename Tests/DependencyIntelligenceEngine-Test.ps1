$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$required = @(
    'src\RimForge.Core\Models\DependencyIntelligenceModels.cs',
    'src\RimForge.Analysis\Services\DependencyIntelligenceService.cs'
)
foreach ($relative in $required) {
    if (-not (Test-Path (Join-Path $root $relative))) { throw "Missing dependency intelligence file: $relative" }
}
$service = Get-Content (Join-Path $root 'src\RimForge.Analysis\Services\DependencyIntelligenceService.cs') -Raw
$models = Get-Content (Join-Path $root 'src\RimForge.Core\Models\DependencyIntelligenceModels.cs') -Raw
foreach ($token in @('TransitiveDependencies','TransitiveDependents','RemovalImpact','OrphanCandidates','ConfidencePercent')) {
    if (-not $models.Contains($token)) { throw "Missing dependency intelligence contract: $token" }
}
foreach ($token in @('Traverse(','removalImpact','orphanCandidates','DependencyIntelligenceReport(')) {
    if (-not $service.Contains($token)) { throw "Missing dependency intelligence behavior: $token" }
}
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
if (-not $composition.Contains('new DependencyIntelligenceService()')) { throw 'Dependency intelligence service is not composed.' }
$inspector = Get-Content (Join-Path $root 'src\RimForge.App\Features\ModInspector\ModInspectorView.xaml') -Raw
foreach ($token in @('DEPENDENCY INTELLIGENCE','SelectedWhyEnabledText','SelectedRemovalImpactText','SelectedDependencyConfidenceText')) {
    if (-not $inspector.Contains($token)) { throw "Inspector is missing dependency intelligence binding: $token" }
}
Write-Host 'Dependency intelligence engine validation passed.' -ForegroundColor Green
