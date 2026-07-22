$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$model = Get-Content (Join-Path $root 'src\RimForge.Core\Models\ForgeEvidencePlatformModels.cs') -Raw
$pipeline = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ForgeEvidencePipeline.cs') -Raw
$bus = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ForgeEvidenceBus.cs') -Raw
$store = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ForgeEvidencePersistence.cs') -Raw
$service = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ForgeEvidenceService.cs') -Raw
$producers = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\IntegratedForgeEvidenceProducers.cs') -Raw
$main = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
$runtime = Get-Content (Join-Path $root 'src\RimForge.App\Features\SharedEvidence\MainWindow.RuntimeEvidence.cs') -Raw
$graph = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ForgeGraphProjectionService.cs') -Raw

foreach ($contract in @('IForgeEvidenceProducer', 'ForgeEvidenceProducerResult', 'ProducerId')) {
    if (-not $model.Contains($contract)) { throw "Missing producer contract: $contract" }
}
foreach ($contract in @('ForgeEvidenceContributionMerger.Merge', 'CompletedSourceKinds', 'ValidateProducerResult')) {
    if (-not $pipeline.Contains($contract)) { throw "Missing aggregation behavior: $contract" }
}
foreach ($contract in @('IForgeEvidenceBus', 'ForgeEvidencePublicationReason', 'Published', 'cannot replace generation')) {
    if (-not $bus.Contains($contract)) { throw "Missing publication behavior: $contract" }
}
foreach ($contract in @('IForgeEvidenceStore', 'FileOptions.WriteThrough', 'RecoveredFromBackup', 'Quarantine', '.bak')) {
    if (-not $store.Contains($contract)) { throw "Missing durable-store behavior: $contract" }
}
foreach ($producer in @('HarmonyMetadataEvidenceProducer', 'CommunityRuleEvidenceProducer', 'UseThisInsteadEvidenceProducer', 'RuntimeCompanionEvidenceProducer', 'CompatibilityIntelligenceEvidenceProducer')) {
    if (-not $producers.Contains($producer)) { throw "Missing integrated evidence producer: $producer" }
}
if ($service.Contains('SnapshotPublished')) { throw 'ForgeEvidenceService still exposes a parallel publication channel.' }
if (-not $main.Contains('_forgeEvidenceBus.Published += ForgeEvidenceBus_Published')) { throw 'The main client is not subscribed to the evidence bus.' }
if ($runtime.Contains('BuildRuntimeGraphEdges')) { throw 'The client still owns a parallel runtime graph projection.' }
if (-not $runtime.Contains('RuntimeEvidenceChanged')) { throw 'Runtime acquisition does not invalidate unified evidence.' }
if (-not $graph.Contains('ProjectEvidenceRelationships')) { throw 'ForgeView does not project runtime relationships from unified evidence.' }

Write-Output 'Epic A Pass 2 unified evidence architecture verified.'
