$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Models/ForgeGraphModels.cs')
$contracts = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Services/NativeEngineInterfaces.cs')
$implementation = Get-Content -Raw (Join-Path $root 'src/RimForge.Infrastructure/Services/ForgeGraphProjectionService.cs')

foreach ($token in @('ForgeGraphProjection', 'ForgeGraphDiff', 'ForgeGraphCluster', 'ForgeGraphIntelligence', 'ForgeGraphProjectionMetrics')) {
    if ($models -notmatch "record $token") { throw "Core ForgeView model is missing: $token" }
    if ($implementation -match "record $token") { throw "Infrastructure still owns ForgeView model: $token" }
}
if ($contracts -notmatch 'interface IForgeGraphProjectionService') { throw 'Core graph projection contract is missing.' }
if ($implementation -match 'interface IForgeGraphProjectionService') { throw 'Infrastructure still owns the graph projection contract.' }
if ($implementation -notmatch 'class ForgeGraphProjectionService : IForgeGraphProjectionService') { throw 'Infrastructure graph implementation is disconnected from the Core contract.' }

Write-Host 'Epic D Pass 1 graph domain boundary verified.'
