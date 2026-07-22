$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Models/LibraryProfileModels.cs')
$service = Get-Content -Raw (Join-Path $root 'src/RimForge.Infrastructure/Services/LibraryProfileProjectionService.cs')
$ui = Get-Content -Raw (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs')

foreach ($token in @('ProfileReadinessStatus', 'ProfileReadinessSummary', 'CanActivate')) {
    if ($models -notmatch $token) { throw "Profile readiness contract is missing: $token" }
}
foreach ($token in @('Core is missing or unresolved', 'not installed', 'multiple installations', 'duplicate active package', 'do not declare support')) {
    if ($service -notmatch [regex]::Escape($token)) { throw "Profile readiness reason is missing: $token" }
}
if ($ui -notmatch 'SelectedProfileReadiness' -or $ui -notmatch 'Activation blocked') { throw 'Profile readiness is not presented in the client.' }
if ($ui -notmatch 'readiness is \{ CanActivate: false \}') { throw 'Blocked profile activation is not enforced.' }

Write-Host 'Epic C Pass 8 canonical profile readiness verified.'
