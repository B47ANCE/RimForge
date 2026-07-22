$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$core = Get-Content (Join-Path $repo 'src/RimForge.Core/Models/ForgeEvidencePlatformModels.cs') -Raw
$service = Get-Content (Join-Path $repo 'src/RimForge.Infrastructure/Services/ForgeEvidenceService.cs') -Raw
$persistence = Get-Content (Join-Path $repo 'src/RimForge.Infrastructure/Services/ForgeEvidencePersistence.cs') -Raw
$protocol = Get-Content (Join-Path $repo 'src/RimForge.Protocol/Contracts/EvidencePayloads.cs') -Raw

@(
    'ForgeEvidenceSchema',
    'ForgeEvidenceContribution',
    'ForgeEvidenceProvenance',
    'ForgeEvidenceIngestionBatch',
    'IForgeEvidenceProducer'
) | ForEach-Object { if (-not $core.Contains($_)) { throw "Missing Forge Evidence platform contract: $_" } }

@(
    'Task<ForgeEvidenceIngestionResult> IngestAsync',
    'Task<ForgeEvidenceSnapshot?> RestoreAsync',
    'ForgeEvidenceContributionMerger.Merge',
    '_evidenceStore.SaveAsync'
) | ForEach-Object { if (-not $service.Contains($_)) { throw "Missing Forge Evidence service integration: $_" } }

@(
    'PersistedForgeEvidenceDocument',
    'SchemaVersion',
    'FileOptions.WriteThrough',
    'IForgeEvidenceStore',
    'RecoveredFromBackup',
    'Quarantine'
) | ForEach-Object { if (-not $persistence.Contains($_)) { throw "Missing Forge Evidence persistence behavior: $_" } }

if (-not $protocol.Contains('ForgeEvidenceIngestionBatchPayload')) { throw 'Protocol ingestion payload is missing.' }
Write-Host 'Epic1ForgeEvidencePlatform-Test: PASS'
