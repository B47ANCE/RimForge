$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content (Join-Path $root 'src\RimForge.Analysis\Models\AnalysisExecutionModels.cs') -Raw
$contract = Get-Content (Join-Path $root 'src\RimForge.Analysis\Services\IModAnalysisEngine.cs') -Raw
$engine = Get-Content (Join-Path $root 'src\RimForge.Analysis\Services\ModAnalysisEngine.cs') -Raw
$dna = Get-Content (Join-Path $root 'src\RimForge.Analysis\Services\ForgeDnaService.cs') -Raw

foreach ($token in @('ModAnalysisRequest','ModAnalysisResult','AnalysisExecutionMetrics','AnalysisDiagnostic','AnalysisProgress','InstalledLibraryCount','ActiveProfileCount','InputFingerprint')) {
    if (-not $models.Contains($token)) { throw "Missing analysis execution model: $token" }
}
foreach ($token in @('Task<ModAnalysisResult> AnalyzeAsync','CancellationToken cancellationToken')) {
    if (-not $contract.Contains($token)) { throw "Missing analysis execution contract: $token" }
}
foreach ($token in @('OrderBy(mod => mod.PackageId','ThenBy(mod => mod.RootPath','CreateInputFingerprint','SHA256.HashData','cancellationToken.ThrowIfCancellationRequested()')) {
    if (-not $engine.Contains($token)) { throw "Missing deterministic/cancellable analysis behavior: $token" }
}
if (-not $dna.Contains('_analysisEngine.AnalyzeAsync(') -or -not $dna.Contains('analysisResult.Snapshot')) {
    throw 'Forge DNA does not consume the canonical asynchronous analysis result.'
}
if ($dna.Contains('_analysisEngine.Analyze(mods,')) { throw 'Forge DNA still invokes the legacy synchronous analysis entry point.' }

Write-Output 'Epic B Pass 1 analysis execution foundation verified.'
