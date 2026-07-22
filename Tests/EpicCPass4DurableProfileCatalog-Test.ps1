$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$model = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Models/ProfileCatalogState.cs')
$contract = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Services/NativeEngineInterfaces.cs')
$store = Get-Content -Raw (Join-Path $root 'src/RimForge.Infrastructure/Services/ProfileCatalogStateStore.cs')
$ui = Get-Content -Raw (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs')
$composition = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Composition/RimForgeApplicationServices.cs')

if ($model -notmatch 'ProfileCatalogState') { throw 'Typed profile catalog state is missing.' }
if ($contract -notmatch 'IProfileCatalogStateStore') { throw 'Profile catalog persistence contract is missing.' }
if ($store -notmatch 'ProfileCatalogState.json' -or $store -notmatch 'ProfileShellState.json') { throw 'Canonical storage or legacy migration is missing.' }
if ($store -notmatch 'File.Move\(stagedPath, path, true\)' -or $store -notmatch 'Distinct\(StringComparer.OrdinalIgnoreCase\)') { throw 'Atomic persistence or deterministic normalization is missing.' }
if ($ui -match 'JsonDocument.Parse\(File.ReadAllText\(ProfileShellStatePath\)\)') { throw 'MainWindow still owns profile catalog JSON parsing.' }
if ($composition -notmatch 'new ProfileCatalogStateStore\(\)') { throw 'Profile catalog store is not registered.' }

Write-Host 'Epic C Pass 4 durable profile catalog verified.'
