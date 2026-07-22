$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$modelsPath = Join-Path $root 'src/RimForge.Analysis/Models/RepairModels.cs'
$plannerPath = Join-Path $root 'src/RimForge.Analysis/Services/RepairPlanner.cs'
$workflowPath = Join-Path $root 'src/RimForge.App/Features/IssueViewer/MainWindow.IssueViewer.cs'
$models = Get-Content -Raw $modelsPath
$planner = Get-Content -Raw $plannerPath
$workflow = Get-Content -Raw $workflowPath

foreach ($token in @('RepairConfidence', 'RepairSafetyClass', 'RepairEvidenceReference', 'RepairPrecondition',
    'RepairPreview', 'DeterministicKey', 'PreconditionsSatisfied', 'CanExecute')) {
    if (-not $models.Contains($token)) { throw "Epic E Pass 1 immutable plan contract is missing: $token" }
}
foreach ($token in @('CaptureContext(', 'BlockedByPreconditions', 'CanonicalAnalysis', 'RepairRecommendation',
    'PerformsWrites', 'OrderBy(id => id', 'ToLowerInvariant')) {
    if (-not $planner.Contains($token)) { throw "Epic E Pass 1 deterministic planning contract is missing: $token" }
}
foreach ($token in @('if (!plan.CanExecute)', 'Repair blocked before mutation', 'BuildRepairPreviewDetails',
    'Confidence:', 'Precondition')) {
    if (-not $workflow.Contains($token)) { throw "Epic E Pass 1 no-write/execution boundary is missing: $token" }
}

dotnet run --project (Join-Path $root 'Tests/RimForge.ExecutionTests/RimForge.ExecutionTests.csproj') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Epic E Pass 1 executable planning coverage failed.' }

Write-Host 'Epic E Pass 1 deterministic repair planning verified.'
