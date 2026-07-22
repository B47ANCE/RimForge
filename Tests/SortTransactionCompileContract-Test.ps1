$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$path = Join-Path $repoRoot 'src\RimForge.Analysis\Services\SortTransactionService.cs'
$content = Get-Content -LiteralPath $path -Raw

if ($content -notmatch 'saved\.UpdatedProfile') {
    throw 'SortTransactionService must use LoadOrderSaveResult.UpdatedProfile.'
}

if ($content -match 'saved\.Profile') {
    throw 'SortTransactionService still references the nonexistent LoadOrderSaveResult.Profile property.'
}

Write-Host 'Sort transaction compile contract passed.' -ForegroundColor Green
