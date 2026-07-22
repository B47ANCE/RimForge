$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$request = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Models/AnalysisExecutionModels.cs') -Raw
$models = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Models/AnalysisModels.cs') -Raw
$engine = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Services/ModAnalysisEngine.cs') -Raw
$dna = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Services/ForgeDnaService.cs') -Raw
$client = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.App/Features/IssueViewer/MainWindow.IssueViewer.cs') -Raw
$roadmap = Get-Content -LiteralPath (Join-Path $root 'ROADMAP.md') -Raw

if (-not $request.Contains('IReadOnlyList<ForgeEvidenceContribution>? Evidence')) {
    throw 'Canonical analysis requests do not accept unified Forge Evidence.'
}
foreach ($token in @('ObservedConflict','CompatibilityEvidenceConcern','SourceIdentity')) {
    if (-not $models.Contains($token)) { throw "Evidence-backed analysis model is missing: $token" }
}
foreach ($token in @('AddEvidenceFindings','TryClassifyEvidence','EvidenceSeverity','item.EffectiveAttributes','--evidence--','RuntimePerformanceRegression','RuntimeIntegrationFailure')) {
    if (-not $engine.Contains($token)) { throw "Evidence convergence implementation is incomplete: $token" }
}
if (-not $dna.Contains('Evidence: evidence')) { throw 'Forge DNA does not pass unified evidence into canonical analysis.' }
if ($client.Contains('BuildRuntimeIssueItems') -or $client.Contains('BuildForgeEvidenceIssueItems')) {
    throw 'Issue Viewer retains a parallel evidence interpretation path.'
}
if (-not $client.Contains('IssueItems.ReplaceAll(_issueViewerSnapshot.Issues)')) {
    throw 'Issue Viewer does not consume only the canonical issue snapshot.'
}
if (-not $roadmap.Contains('### Pass 5: unified evidence analysis')) {
    throw 'Epic B Pass 5 is not recorded in the roadmap.'
}

Write-Output 'Epic B Pass 5 unified evidence analysis verified.'
