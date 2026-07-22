$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$solver = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/LockedPositionSolver.cs') -Raw
$engine = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/ModAnalysisEngine.cs') -Raw
$model = Get-Content (Join-Path $root 'src/RimForge.Core/Models/UserLoadOrderLock.cs') -Raw

foreach ($needle in @('LockedPositionSolver', 'FindLegalPositions', 'LoadOrderLockConflict', 'SuggestedPositions')) {
    if (($solver + $model) -notmatch [regex]::Escape($needle)) { throw "Missing lock contract: $needle" }
}
if ($engine -notmatch 'UserLockConflict') { throw 'ModAnalysisEngine does not publish user lock conflicts.' }
if ($engine -notmatch 'Profile workspace lock') { throw 'Load-order provenance does not identify profile locks.' }
Write-Host 'User locked positions contract passed.' -ForegroundColor Green
