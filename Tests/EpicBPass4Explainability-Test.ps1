$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Models/AnalysisExplainabilityModels.cs') -Raw
$result = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Models/AnalysisExecutionModels.cs') -Raw
$builder = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Services/AnalysisExplanationBuilder.cs') -Raw
$engine = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Services/ModAnalysisEngine.cs') -Raw
$roadmap = Get-Content -LiteralPath (Join-Path $root 'ROADMAP.md') -Raw

foreach ($token in @('AnalysisOverview','AnalysisRelationshipRationale','ModAnalysisExplanation','AnalysisExplanationCatalog','GetMod')) {
    if (-not $models.Contains($token)) { throw "Missing analysis explainability contract: $token" }
}
foreach ($token in @('HealthyModCount','AffectedModCount','MandatoryRelationshipCount','AdvisoryRelationshipCount','HasCompleteLoadOrder','Narrative')) {
    if (-not $models.Contains($token)) { throw "Analysis overview is incomplete: $token" }
}
foreach ($token in @('Diagnostics','Relationships','Recommendations','LoadOrderDecision','IsActive')) {
    if (-not $models.Contains($token)) { throw "Per-mod explanation is incomplete: $token" }
}
if (-not $result.Contains('AnalysisExplanationCatalog Explainability')) {
    throw 'Canonical analysis results do not expose explainability.'
}
foreach ($token in @('AnalysisExplanationBuilder.Build','cached.Explainability','StoreCached(fingerprint, snapshot, diagnostics, explainability')) {
    if (-not $engine.Contains($token)) { throw "Explainability is not integrated with execution/cache ownership: $token" }
}
if (-not $builder.Contains('OrderBy(item => item.PackageId') -or -not $builder.Contains('StringComparer.OrdinalIgnoreCase')) {
    throw 'Per-mod explanations are not emitted deterministically.'
}
if (-not $roadmap.Contains('### Pass 4: analysis explainability')) {
    throw 'Epic B Pass 4 is not recorded in the roadmap.'
}

Write-Output 'Epic B Pass 4 analysis explainability verified.'
