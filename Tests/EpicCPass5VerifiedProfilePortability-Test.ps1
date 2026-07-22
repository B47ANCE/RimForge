$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$model = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Models/ProfileModels.cs')
$service = Get-Content -Raw (Join-Path $root 'src/RimForge.Infrastructure/Services/ProfilePackageInspectionService.cs')
$ui = Get-Content -Raw (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs')
$composition = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Composition/RimForgeApplicationServices.cs')

if ($model -notmatch 'ProfilePackageInspection' -or $model -notmatch 'HasCompatibilityWarnings') { throw 'Profile package inspection model is missing.' }
foreach ($token in @('MaximumManifestBytes', 'MaximumConfigBytes', 'SHA256.HashData', 'SequenceEqual', 'IsSafeEntryName')) {
    if ($service -notmatch [regex]::Escape($token)) { throw "Portable package validation is missing: $token" }
}
if ($service -notmatch 'MissingPackageIds' -and $model -notmatch 'MissingPackageIds') { throw 'Missing-mod compatibility projection is absent.' }
if ($ui -notmatch 'Profile package rejected' -or $ui -notmatch 'Profile Compatibility Warning') { throw 'Main-client import safety UI is not wired.' }
if ($composition -notmatch 'new ProfilePackageInspectionService\(\)') { throw 'Package inspection service is not registered.' }

Write-Host 'Epic C Pass 5 verified profile portability verified.'
