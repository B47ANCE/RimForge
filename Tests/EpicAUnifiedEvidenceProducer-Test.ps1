$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$model = Get-Content (Join-Path $root 'src\RimForge.Core\Models\ForgeEvidencePlatformModels.cs') -Raw
$pipeline = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ForgeEvidencePipeline.cs') -Raw
$factory = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\IntegratedForgeEvidenceProducers.cs') -Raw
$persistence = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ForgeEvidencePersistence.cs') -Raw

foreach ($required in @('IForgeEvidenceProducer', 'ForgeEvidenceProducerResult', 'ForgeEvidenceProducerProgress', 'ForgeEvidenceProducerDiagnostic', 'ProducerId')) {
    if (-not $model.Contains($required)) { throw "Unified producer model is missing $required." }
}
if (-not $pipeline.Contains('IReadOnlyList<IForgeEvidenceProducer>')) { throw 'The pipeline is not producer-backed.' }
if (-not $pipeline.Contains('RF-EVIDENCE-PRODUCER-FAILED')) { throw 'Producer failures do not use the unified diagnostic code.' }
if (-not $factory.Contains('ForgeEvidenceProducerFactory')) { throw 'The built-in producer factory is missing.' }
if (-not $persistence.Contains('JsonPropertyName("contributorDiagnostics")')) { throw 'Snapshot compatibility for pre-migration diagnostics was not preserved.' }

$legacy = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter '*.cs' -File |
    Select-String -Pattern 'IForgeEvidenceContributor|ForgeEvidenceContributor|ContributorId'
if ($legacy) { throw "Legacy evidence contributor contracts remain: $($legacy -join '; ')" }

Write-Output 'Epic A unified evidence producer contract verified.'
