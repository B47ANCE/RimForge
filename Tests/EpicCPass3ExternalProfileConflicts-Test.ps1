$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Models/ExternalProfileReconciliation.cs')
$service = Get-Content -Raw (Join-Path $root 'src/RimForge.Infrastructure/Services/ExternalProfileConflictService.cs')
$ui = Get-Content -Raw (Join-Path $root 'src/RimForge.App/MainWindow.ExternalProfileChanges.cs')
$actions = Get-Content -Raw (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs')
$composition = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Composition/RimForgeApplicationServices.cs')

foreach ($token in @('AdoptExternal', 'RestoreRimForge', 'Defer', 'RequiresAcknowledgement')) {
    if ($models -notmatch $token) { throw "External resolution model is missing: $token" }
}
if ($service -notmatch 'SaveLoadOrderAsync' -or $service -notmatch 'ActivateAsync') { throw 'Conflict resolution bypasses canonical persistence or activation.' }
if ($service -notmatch 'No files were changed' -or $service -notmatch 'IsLocked') { throw 'No-write deferral or locked-profile protection is missing.' }
if ($ui -notmatch 'defer-external-profile' -or $actions -notmatch 'DeferExternalProfileAsync') { throw 'Main-client defer action is not wired.' }
if ($composition -notmatch 'new ExternalProfileConflictService\(profileWorkspaceService\)') { throw 'Conflict service is not registered.' }

Write-Host 'Epic C Pass 3 external profile conflict resolution verified.'
