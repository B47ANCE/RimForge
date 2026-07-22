$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Models/AnalysisExecutionModels.cs') -Raw
$engine = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Services/ModAnalysisEngine.cs') -Raw
$roadmap = Get-Content -LiteralPath (Join-Path $root 'ROADMAP.md') -Raw

foreach ($token in @('enum AnalysisStage','AnalysisStageMetrics','IReadOnlyList<AnalysisStageMetrics> Stages','PackageId','RelatedPackageIds')) {
    if (-not $models.Contains($token)) { throw "Missing observable analysis contract: $token" }
}
foreach ($stage in @('Indexing','Relationships','Rules','GraphValidation','ProfileValidation','LoadOrderPlanning','Finalizing','Complete')) {
    if (-not $engine.Contains("AnalysisStage.$stage")) { throw "Analysis stage is not instrumented: $stage" }
}
foreach ($token in @('request.LockedPositions','mod.Dependencies.Select','mod.LoadBefore','mod.LoadAfter','cancellationToken.ThrowIfCancellationRequested()')) {
    if (-not $engine.Contains($token)) { throw "Analysis input/cancellation contract is incomplete: $token" }
}
if (-not $roadmap.Contains('### Pass 2: observable analysis pipeline')) {
    throw 'Epic B Pass 2 is not recorded in the roadmap.'
}

Write-Output 'Epic B Pass 2 observable analysis pipeline verified.'
