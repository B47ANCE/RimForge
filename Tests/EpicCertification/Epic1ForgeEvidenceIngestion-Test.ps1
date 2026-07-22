$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$core = Get-Content (Join-Path $repo 'src/RimForge.Core/Models/ForgeEvidencePlatformModels.cs') -Raw
$pipeline = Get-Content (Join-Path $repo 'src/RimForge.Infrastructure/Services/ForgeEvidencePipeline.cs') -Raw
$producers = Get-Content (Join-Path $repo 'src/RimForge.Infrastructure/Services/ForgeEvidenceProducers.cs') -Raw
$service = Get-Content (Join-Path $repo 'src/RimForge.Infrastructure/Services/ForgeEvidenceService.cs') -Raw
$persistence = Get-Content (Join-Path $repo 'src/RimForge.Infrastructure/Services/ForgeEvidencePersistence.cs') -Raw
$index = Get-Content (Join-Path $repo 'src/RimForge.Infrastructure/Services/ForgeEvidenceIndex.cs') -Raw
$docs = Get-Content (Join-Path $repo 'docs/architecture/FORGE_EVIDENCE_INGESTION.md') -Raw

@(
    'ForgeEvidenceCollectionContext',
    'ForgeEvidenceProducerResult',
    'ForgeEvidenceProducerDiagnostic',
    'int Order',
    'ForgeEvidenceSchema'
) | ForEach-Object { if (-not $core.Contains($_)) { throw "Missing ingestion contract: $_" } }

@(
    'IForgeEvidencePipeline',
    'MaximumProducerAttempts',
    'ValidateProducerResult',
    'ForgeEvidenceValidationException',
    'RF-EVIDENCE-PRODUCER-FAILED'
) | ForEach-Object { if (-not $pipeline.Contains($_)) { throw "Missing ingestion pipeline behavior: $_" } }

@(
    'StaticModMetadataEvidenceProducer',
    'DependencyMetadataEvidenceProducer',
    'required-dependency',
    'declared-incompatibility'
) | ForEach-Object { if (-not $producers.Contains($_)) { throw "Missing built-in producer behavior: $_" } }

if (-not $service.Contains('_pipeline.CollectAsync')) { throw 'ForgeEvidenceService does not use the unified pipeline.' }
if (-not $service.Contains('CreateDefaultProducers')) { throw 'Default evidence producers are not registered.' }
if ($persistence.Contains('Guid.NewGuid().ToString("N")')) { throw 'Generated evidence IDs remain nondeterministic.' }
if (-not $persistence.Contains('SHA256.HashData')) { throw 'Deterministic SHA-256 evidence IDs are missing.' }

@('ForSubject', 'ForType', 'FromSource', 'Between') | ForEach-Object {
    if (-not $index.Contains($_)) { throw "Missing evidence index query: $_" }
}

if (-not $docs.Contains('Transaction lifecycle')) { throw 'Forge Evidence ingestion documentation is incomplete.' }
Write-Host 'Epic1ForgeEvidenceIngestion-Test: PASS'
