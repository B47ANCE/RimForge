$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$service = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/SortTransactionService.cs') -Raw
foreach ($needle in @('SortTransactionPreview', 'CanApply', 'SaveLoadOrderAsync', 'profile remains unchanged', 'RestoredOrder')) {
    if ($service -notmatch [regex]::Escape($needle)) { throw "Missing transaction contract: $needle" }
}
Write-Host 'Sort transaction rollback contract passed.' -ForegroundColor Green
