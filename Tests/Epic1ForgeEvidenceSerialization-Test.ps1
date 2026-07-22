$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$modelPath = Join-Path $root 'src/RimForge.Core/Models/ForgeEvidencePlatformModels.cs'
$queryPath = Join-Path $root 'src/RimForge.Core/Models/ForgeEvidenceQueryModels.cs'
$payloadPath = Join-Path $root 'src/RimForge.Protocol/Contracts/EvidencePayloads.cs'

$model = Get-Content $modelPath -Raw
$query = Get-Content $queryPath -Raw
$payload = Get-Content $payloadPath -Raw

if ($model -notmatch 'CurrentVersion\s*=\s*2') { throw 'Forge Evidence schema version 2 contract is missing.' }
if ($model -notmatch 'ForgeEvidenceIngestionBatch') { throw 'Forge Evidence ingestion batch contract is missing.' }
if ($query -notmatch 'sealed record ForgeEvidenceQuery') { throw 'Forge Evidence query serialization contract is missing.' }
if ($query -notmatch 'sealed record ForgeEvidenceDiagnostics') { throw 'Forge Evidence diagnostics serialization contract is missing.' }
if ($payload -notmatch 'Evidence') { throw 'Protocol evidence payload contract is missing.' }

$forbidden = @('System.Windows', 'RimForge.App', 'RimForge.Infrastructure')
foreach ($namespace in $forbidden) {
    if ($query -match [regex]::Escape($namespace)) {
        throw "Core query contract incorrectly depends on $namespace."
    }
}

Write-Host 'Epic 1 Forge Evidence serialization and protocol contracts passed.'
