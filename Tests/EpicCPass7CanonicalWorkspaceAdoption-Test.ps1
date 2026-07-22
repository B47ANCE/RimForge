$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$main = Get-Content -Raw (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs')
$sorter = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Features/ModSorter/MainWindow.ModSorter.cs')
$issues = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Features/IssueViewer/MainWindow.IssueViewer.cs')

if ($main -notmatch 'LibraryProfileWorkspaceSnapshot _libraryProfileWorkspace') { throw 'Main client does not own a canonical workspace snapshot.' }
if ($main -notmatch '_libraryProfileProjectionService.Create') { throw 'Canonical workspace is not projected through the shared service.' }
if ($main -notmatch 'selectedProjection\?\.ActiveMods' -or $main -notmatch 'selectedProjection.InactiveInstalledMods') { throw 'Profile rows do not consume canonical active/inactive projections.' }
if ($main -notmatch 'Profiles.ReplaceAll\(ordered\);\s*RefreshLibraryProfileWorkspace\(\)') { throw 'Profile loading does not refresh canonical workspace state.' }
if ($sorter -notmatch 'RefreshLibraryProfileWorkspace\(\)' -or $issues -notmatch 'RefreshLibraryProfileWorkspace\(\)') { throw 'In-place profile saves leave canonical workspace state stale.' }
if ($main -notmatch '_libraryProfileWorkspace.Fingerprint') { throw 'Workspace fingerprint is not surfaced diagnostically.' }

Write-Host 'Epic C Pass 7 canonical workspace adoption verified.'
