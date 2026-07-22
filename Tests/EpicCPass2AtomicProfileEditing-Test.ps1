$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Models/ProfileEditModels.cs')
$service = Get-Content -Raw (Join-Path $root 'src/RimForge.Infrastructure/Services/ProfileEditService.cs')
$composition = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Composition/RimForgeApplicationServices.cs')

if ($models -notmatch 'ProfileEditDraft' -or $models -notmatch 'ProfileEditChangeSet') { throw 'Profile edit transaction models are missing.' }
if ($service -notmatch 'BaseWorkspaceFingerprint' -or $service -notmatch 'IsStale: true') { throw 'Stale edit protection is missing.' }
if ($service -notmatch 'SaveLoadOrderAsync') { throw 'Profile commits bypass canonical persistence.' }
foreach ($required in @('is locked', 'cannot be empty', 'Core must remain', 'appears more than once', 'is not installed', 'multiple installed mods')) {
    if ($service -notmatch [regex]::Escape($required)) { throw "Profile edit validation is missing: $required" }
}
if ($composition -notmatch 'new ProfileEditService\(profileWorkspaceService\)') { throw 'Profile edit service is not registered in canonical composition.' }

Write-Host 'Epic C Pass 2 atomic profile editing verified.'
