$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$mainWindow = Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs'
$source = Get-Content $mainWindow -Raw

if ($source -notmatch 'Authoritative Forge evidence generation') {
    throw 'MainWindow Forge flow is missing authoritative Forge evidence generation.'
}
if ($source -notmatch 'forceRescan:\s*true') {
    throw 'MainWindow Forge flow must force a fresh authoritative evidence generation.'
}
if ($source -match 'if \(_analysisSnapshot is null\)\s*\{\s*ModSorterItems\.ReplaceAll\(Array\.Empty<ModSorterItemViewModel>\(\)\)') {
    throw 'Mod Sorter still clears the authoritative library when analysis is unavailable.'
}
if ($source -notmatch 'BuildModSorterItems\(Mods, activeLoadOrder, _analysisSnapshot\)') {
    throw 'Mod Sorter is not using the analysis-optional authoritative library projection.'
}
if ($source -notmatch 'ShowIssuesOnly && analysisSnapshot is not null') {
    throw 'Pending analysis can still hide the authoritative library behind the issues-only filter.'
}

if ($source -notmatch 'Publish the authoritative library projection before profile-scoped analysis') {
    throw 'Forge does not publish the authoritative library projection before analysis.'
}
if ($source -notmatch 'A failed analysis must not blank the workspace') {
    throw 'Forge failure recovery does not preserve the authoritative workspace projection.'
}

Write-Host 'Authoritative Forge projection validation passed.' -ForegroundColor Green
