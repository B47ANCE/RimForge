$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$modelsPath = Join-Path $root 'src/RimForge.Analysis/Models/RepairModels.cs'
$executorPath = Join-Path $root 'src/RimForge.Analysis/Services/RepairTransactionExecutor.cs'
$workflowPath = Join-Path $root 'src/RimForge.App/Features/IssueViewer/MainWindow.IssueViewer.cs'
$viewPath = Join-Path $root 'src/RimForge.App/Features/IssueViewer/IssueViewerView.xaml'
$models = Get-Content -Raw $modelsPath
$executor = Get-Content -Raw $executorPath
$workflow = Get-Content -Raw $workflowPath
$view = Get-Content -Raw $viewPath

foreach ($token in @('RepairTransactionState', 'RepairAuditEvent', 'RepairTransactionJournal',
    'RepairExecutionResult', 'RecoveryRequired', 'IsTerminal')) {
    if (-not $models.Contains($token)) { throw "Epic E Pass 2 transaction model is missing: $token" }
}
foreach ($token in @('IRepairTransactionExecutor', 'SemaphoreSlim', 'WriteAtomicAsync', 'RollingBack',
    'OperationCanceledException', 'DiscoverInterrupted', 'RecoverAsync', 'CancellationToken.None')) {
    if (-not $executor.Contains($token)) { throw "Epic E Pass 2 executor contract is missing: $token" }
}
foreach ($token in @('RepairTransactionExecutor.ExecuteAsync', 'CalculateCanonicalLoadOrder',
    'LastRepairOutcomeText', 'Repair committed', 'View Audit', 'SelectedProfileReadiness',
    'ForgeFocusedProvenanceSummary', 'InspectInterruptedRepairTransactions')) {
    if (-not $workflow.Contains($token)) { throw "Epic E Pass 2 workflow integration is missing: $token" }
}
if (-not $view.Contains('AutomationProperties.LiveSetting="Polite"')) {
    throw 'Epic E Pass 2 user-visible live outcome reporting is missing.'
}

dotnet run --project (Join-Path $root 'Tests/RimForge.ExecutionTests/RimForge.ExecutionTests.csproj') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Epic E Pass 2 executable transaction coverage failed.' }

Write-Host 'Epic E Pass 2 transactional repair execution verified.'
