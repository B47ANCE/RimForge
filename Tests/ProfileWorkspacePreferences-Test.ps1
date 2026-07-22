$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$store = Get-Content (Join-Path $root 'src/RimForge.Infrastructure/Services/ProfileWorkspacePreferencesStore.cs') -Raw
foreach ($needle in @('WorkspacePreferences.json', 'LockedPositions', 'DismissedRecommendationIds', 'ExpandedExplanationPackageIds', 'File.Move(staged, path, true)')) {
    if ($store -notmatch [regex]::Escape($needle)) { throw "Missing workspace preference contract: $needle" }
}
Write-Host 'Profile workspace preferences contract passed.' -ForegroundColor Green
