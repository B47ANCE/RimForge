$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$model = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Models/LibraryProfileModels.cs')
$contract = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Services/NativeEngineInterfaces.cs')
$service = Get-Content -Raw (Join-Path $root 'src/RimForge.Infrastructure/Services/LibraryProfileProjectionService.cs')
$composition = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Composition/RimForgeApplicationServices.cs')

if ($model -notmatch 'ProfileModResolution' -or $model -notmatch 'LibraryProfileWorkspaceSnapshot') { throw 'Canonical library/profile models are missing.' }
if ($contract -notmatch 'interface ILibraryProfileProjectionService') { throw 'Projection contract is missing.' }
if ($service -notmatch 'ProfileModResolution\.Missing' -or $service -notmatch 'ProfileModResolution\.Ambiguous') { throw 'Resolution classification is incomplete.' }
if ($service -notmatch 'SHA256\.HashData') { throw 'Stable workspace fingerprint is missing.' }
if ($composition -notmatch 'new LibraryProfileProjectionService\(\)') { throw 'Projection service is not registered in canonical composition.' }

Write-Host 'Epic C Pass 1 library/profile projection verified.'
