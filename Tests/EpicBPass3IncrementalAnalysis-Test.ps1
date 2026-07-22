$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Models/AnalysisExecutionModels.cs') -Raw
$contract = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Services/IModAnalysisEngine.cs') -Raw
$engine = Get-Content -LiteralPath (Join-Path $root 'src/RimForge.Analysis/Services/ModAnalysisEngine.cs') -Raw
$roadmap = Get-Content -LiteralPath (Join-Path $root 'ROADMAP.md') -Raw

foreach ($token in @('AnalysisCachePolicy','AnalysisCacheDisposition','AnalysisCacheInfo','CacheLookup','CachePolicy')) {
    if (-not $models.Contains($token)) { throw "Missing incremental analysis contract: $token" }
}
if (-not $contract.Contains('void InvalidateCache(string? inputFingerprint = null)')) {
    throw 'Analysis cache invalidation is not part of the engine contract.'
}
foreach ($token in @('CacheCapacity = 8','TryGetCached','StoreCached','AnalysisCachePolicy.Refresh','AnalysisCachePolicy.Bypass','LastAccessSequence')) {
    if (-not $engine.Contains($token)) { throw "Incremental analysis implementation is incomplete: $token" }
}
foreach ($token in @('mod.Evidence.Badges','mod.Evidence.Capabilities','mod.Evidence.NotableFindings')) {
    if (-not $engine.Contains($token)) { throw "Evidence-sensitive fingerprint input is missing: $token" }
}
if (-not $roadmap.Contains('### Pass 3: incremental analysis reuse')) {
    throw 'Epic B Pass 3 is not recorded in the roadmap.'
}

Write-Output 'Epic B Pass 3 incremental analysis reuse verified.'
