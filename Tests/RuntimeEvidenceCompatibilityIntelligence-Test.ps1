$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$required = @(
  'src/RimForge.Core/Models/RuntimeEvidenceModels.cs',
  'src/RimForge.Infrastructure/Services/RuntimeEvidenceStore.cs',
  'src/RimForge.Infrastructure/Services/CompatibilityIntelligenceService.cs',
  'src/RimForge.Infrastructure/Services/RuntimeSensorHost.cs',
  'src/RimForge.App/Features/SharedEvidence/MainWindow.RuntimeEvidence.cs'
)
foreach ($path in $required) {
  if (-not (Test-Path (Join-Path $root $path))) { throw "Missing runtime evidence component: $path" }
}
$runtime = Get-Content (Join-Path $root 'src/RimForge.App/Features/SharedEvidence/MainWindow.RuntimeEvidence.cs') -Raw
foreach ($token in @('InitializeRuntimeEvidenceAsync','RuntimeEvidenceChanged','SelectedRuntimeEvidence','RuntimeConflictCount')) {
  if ($runtime -notmatch [regex]::Escape($token)) { throw "Runtime evidence integration is missing $token" }
}
$graph = Get-Content (Join-Path $root 'src/RimForge.Infrastructure/Services/ForgeGraphProjectionService.cs') -Raw
foreach ($token in @('ProjectEvidenceRelationships','ForgeEvidenceSourceKind.RuntimeCompanion','ForgeEvidenceSourceKind.CompatibilityIntelligence')) {
  if ($graph -notmatch [regex]::Escape($token)) { throw "Unified runtime graph projection is missing $token" }
}
if ($runtime -match 'BuildRuntimeGraphEdges') { throw 'Parallel runtime graph projection still exists in the main client.' }
$issue = Get-Content (Join-Path $root 'src/RimForge.App/Features/IssueViewer/MainWindow.IssueViewer.cs') -Raw
$analysis = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/ModAnalysisEngine.cs') -Raw
foreach ($token in @('AddEvidenceFindings','RuntimePerformanceRegression','RuntimeIntegrationFailure','RuntimeObservedConflict')) {
  if ($analysis -notmatch [regex]::Escape($token)) { throw "Canonical runtime analysis is missing $token" }
}
if ($issue -match 'BuildRuntimeIssueItems|BuildForgeEvidenceIssueItems') { throw 'Parallel UI evidence issue projection still exists.' }
$inspector = Get-Content (Join-Path $root 'src/RimForge.App/Features/ModInspector/ModInspectorView.xaml') -Raw
if ($inspector -notmatch 'RUNTIME EVIDENCE') { throw 'Inspector runtime evidence section is missing.' }
Write-Host 'Runtime Evidence & Compatibility Intelligence contract passed.'
