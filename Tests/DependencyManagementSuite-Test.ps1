$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$required = @(
    'src\RimForge.Core\Models\DependencyManagementModels.cs',
    'src\RimForge.Core\Services\NativeEngineInterfaces.cs',
    'src\RimForge.Analysis\Services\DependencyManagementService.cs',
    'src\RimForge.App\Composition\RimForgeApplicationServices.cs',
    'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs',
    'src\RimForge.App\Features\Settings\MainWindow.Settings.cs',
    'src\RimForge.App\Features\Settings\SettingsView.xaml'
)
foreach ($relative in $required) {
    if (-not (Test-Path (Join-Path $root $relative))) { throw "Missing Dependency Management Suite file: $relative" }
}

$models = Get-Content (Join-Path $root 'src\RimForge.Core\Models\DependencyManagementModels.cs') -Raw
foreach ($token in @('DependencyActivationPlan','MissingDependencyRequirement','DependencyRemovalPlan','DependencyManagementSummary','OrphanCleanupMode')) {
    if (-not $models.Contains($token)) { throw "Missing dependency management contract: $token" }
}

$interfaces = Get-Content (Join-Path $root 'src\RimForge.Core\Services\NativeEngineInterfaces.cs') -Raw
foreach ($token in @('IDependencyManagementService','PlanActivation','PlanRemoval','FindOrphans','Summarize')) {
    if (-not $interfaces.Contains($token)) { throw "Missing dependency management service contract: $token" }
}

$service = Get-Content (Join-Path $root 'src\RimForge.Analysis\Services\DependencyManagementService.cs') -Raw
foreach ($token in @('traverseActiveRoot','missing.Values','cycles.Add','RemovalImpact','requested.Concat(impacts.Select','FindOrphans','DependencyManagementSummary')) {
    if (-not $service.Contains($token)) { throw "Missing dependency management engine behavior: $token" }
}

$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
foreach ($token in @('IDependencyManagementService','DependencyManagementService','new DependencyManagementService(dependencyIntelligenceService)')) {
    if (-not $composition.Contains($token)) { throw "Dependency management service is not composed correctly: $token" }
}

$sorter = Get-Content (Join-Path $root 'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs') -Raw
foreach ($token in @(
    '_dependencyManagementService.PlanActivation',
    '_dependencyManagementService.PlanRemoval',
    '_dependencyManagementService.FindOrphans',
    'DependencyAssistanceMode.Ask',
    'DependencyAssistanceMode.Manual',
    'OrphanCleanupMode.Automatic',
    'OrphanCleanupMode.Ask',
    'OrphanCleanupMode.Manual',
    'disable-impacted',
    'remove-orphans',
    'RegisterLoadOrderUndo',
    'Missing dependency path:'
)) {
    if (-not $sorter.Contains($token)) { throw "Missing complete dependency workflow behavior: $token" }
}

$settings = Get-Content (Join-Path $root 'src\RimForge.App\Features\Settings\MainWindow.Settings.cs') -Raw
foreach ($token in @('DependencyAssistanceMode','OrphanCleanupMode','OrphanCleanupPreference.ToString()','Enum.TryParse')) {
    if (-not $settings.Contains($token)) { throw "Missing dependency suite persistence: $token" }
}

$settingsXaml = Get-Content (Join-Path $root 'src\RimForge.App\Features\Settings\SettingsView.xaml') -Raw
foreach ($token in @('DependencyAssistanceModes','DependencyAssistancePreference','OrphanCleanupModes','OrphanCleanupPreference')) {
    if (-not $settingsXaml.Contains($token)) { throw "Missing dependency suite settings binding: $token" }
}

$inspector = Get-Content (Join-Path $root 'src\RimForge.App\Features\ModInspector\ModInspectorView.xaml') -Raw
$forgeView = Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml') -Raw
foreach ($token in @('DependencyManagementHealthText','DependencyManagementCountsText')) {
    if (-not $inspector.Contains($token)) { throw "Inspector is missing suite integration: $token" }
    if (-not $forgeView.Contains($token)) { throw "ForgeView is missing suite integration: $token" }
}

Write-Host 'Dependency Management Suite validation passed.' -ForegroundColor Green
