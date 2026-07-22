$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dialog = Get-Content (Join-Path $root 'src\RimForge.UI\Dialogs\ForgeDialogService.cs') -Raw
$xaml = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml') -Raw
$manifest = Get-Content (Join-Path $root 'src\RimForge.UI\Assets\Branding\AssetManifest.json') -Raw
if ($dialog -notmatch 'CreateLaunchCheck\("Active profile"') { throw 'Combined Active Profile readiness card is missing.' }
if ($dialog -match 'CreateLaunchCheck\("Workspace revision"') { throw 'Workspace revision is still rendered as a separate readiness card.' }
if ($dialog -match 'CreateLaunchCheck\("Saved profile"') { throw 'Saved profile is still rendered as a separate readiness card.' }
if ($dialog -notmatch 'LaunchCheckState\.Warning') { throw 'Warning readiness semantics are missing.' }
if ($xaml -match 'Text="RF"') { throw 'Temporary RF navigation mark is still present.' }
if ($xaml -notmatch 'RimForge\.Badge\.png') { throw 'Canonical compact badge is not wired into the application chrome.' }
if ($manifest -notmatch 'ApprovedCanonical') { throw 'Brand manifest does not mark the logo as approved canonical.' }
$badge = Join-Path $root 'src\RimForge.UI\Assets\Branding\Badge\RimForge.Badge.png'
if (-not (Test-Path $badge)) { throw 'Canonical compact badge asset is missing.' }
Write-Host 'Launch readiness branding test passed.'
