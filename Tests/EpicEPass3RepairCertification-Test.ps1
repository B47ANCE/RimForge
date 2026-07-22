$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$policyPath = Join-Path $root 'src/RimForge.Analysis/Services/RepairSafetyPolicy.cs'
$modelsPath = Join-Path $root 'src/RimForge.Analysis/Models/RepairModels.cs'
$executorPath = Join-Path $root 'src/RimForge.Analysis/Services/RepairTransactionExecutor.cs'
$workflowPath = Join-Path $root 'src/RimForge.App/Features/IssueViewer/MainWindow.IssueViewer.cs'
$policy = Get-Content -Raw $policyPath
$models = Get-Content -Raw $modelsPath
$executor = Get-Content -Raw $executorPath
$workflow = Get-Content -Raw $workflowPath

foreach ($token in @('AutomaticAllowlist', '(AnalysisIssueCode.LoadOrderViolation, RepairActionKind.ReorderProfile)',
    'RuntimeObservedConflict', 'RuntimePerformanceRegression', 'RuntimeIntegrationFailure',
    'runtimeEvidence, reason', 'Companion runtime evidence is advisory')) {
    if (-not $policy.Contains($token)) { throw "Epic E Pass 3 safety policy is missing: $token" }
}
foreach ($token in @('RepairCertification', 'RestrictiveDefault', 'AutomaticExecutionAllowlisted',
    'RequiresExplicitConfirmation', 'CertificationPolicyId')) {
    if (-not $models.Contains($token)) { throw "Epic E Pass 3 certification model is missing: $token" }
}
foreach ($token in @('not certified by the active safety policy', 'Automatic repair execution is not allowlisted', 'Runtime evidence cannot authorize',
    'requires explicit user confirmation', 'userConfirmed')) {
    if (-not $executor.Contains($token)) { throw "Epic E Pass 3 execution enforcement is missing: $token" }
}
foreach ($token in @('Certification.AutomaticExecutionAllowlisted: true', 'userConfirmed: true', 'Certification:')) {
    if (-not $workflow.Contains($token)) { throw "Epic E Pass 3 certified UI workflow is missing: $token" }
}

dotnet run --project (Join-Path $root 'Tests/RimForge.ExecutionTests/RimForge.ExecutionTests.csproj') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Epic E Pass 3 executable certification coverage failed.' }

Write-Host 'Epic E Pass 3 Repair Engine certification verified.'
