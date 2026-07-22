$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$producer = Join-Path $root 'src/RimForge.Infrastructure/Services/IntegratedForgeEvidenceProducers.cs'
$composition = Join-Path $root 'src/RimForge.App/Composition/RimForgeApplicationServices.cs'
$content = Get-Content $producer -Raw
@(
  'HarmonyMetadataEvidenceProducer',
  'CommunityRuleEvidenceProducer',
  'UseThisInsteadEvidenceProducer',
  'RuntimeCompanionEvidenceProducer',
  'CompatibilityIntelligenceEvidenceProducer',
  'ForgeEvidenceProducerFactory.Create(runtimeEvidenceStore)'
) | ForEach-Object {
  $target = if ($_ -like '*runtimeEvidenceStore*') { Get-Content $composition -Raw } else { $content }
  if ($target -notmatch [regex]::Escape($_)) { throw "Missing Epic 1 Pass 4 contract: $_" }
}
Write-Host 'Epic 1 Pass 4 evidence producer integration contracts passed.'
