$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$model = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Models/ProfileCatalogState.cs')
$store = Get-Content -Raw (Join-Path $root 'src/RimForge.Infrastructure/Services/ProfileCatalogStateStore.cs')
$ui = Get-Content -Raw (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs')

if ($model -notmatch 'LastSelectedProfileName' -or $model -notmatch 'ShowFullLibrary') { throw 'Workspace continuity state is incomplete.' }
if ($store -notmatch 'NormalizeOptional\(state.LastSelectedProfileName\)') { throw 'Remembered profile identity is not normalized.' }
if ($ui -notmatch 'SelectedProfile\?\.Name \?\? _profileCatalogState.LastSelectedProfileName') { throw 'Startup does not restore remembered profile selection.' }
if ($ui -notmatch '_showFullLibrary = _profileCatalogState.ShowFullLibrary') { throw 'Startup does not restore library scope.' }
if ($ui -notmatch 'SaveProfileShellState\(result.Profile.Name\)') { throw 'Rename does not preserve remembered selection.' }
if ($ui -notmatch 'SelectedProfile = null;\s*SaveProfileShellState\(\)') { throw 'Deletion does not clear remembered selection.' }

Write-Host 'Epic C Pass 6 profile workspace continuity verified.'
