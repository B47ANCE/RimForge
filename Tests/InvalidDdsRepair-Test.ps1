Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

Import-Module `
    (Join-Path $root "Modules\TextureOptimizer.psm1") `
    -Force `
    -ErrorAction Stop

foreach ($command in @(
    "Get-RimForgeDdsHeaderInfo",
    "Get-RimForgeInvalidDdsInventory",
    "New-RimForgeInvalidDdsRepairPlan"
)) {
    if ($null -eq (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Missing invalid-DDS repair command: $command"
    }
}

$geometry = Get-RimForgeTextureGeometry `
    -OriginalWidth 513 `
    -OriginalHeight 301 `
    -DimensionMultiple 4 `
    -TieRule Up

if (
    $geometry.CanvasWidth -ne 512 -or
    $geometry.CanvasHeight -ne 300
) {
    throw "Nearest-divisible-by-four geometry test failed."
}

Write-Host "RimForge invalid DDS repair tests passed." -ForegroundColor Green
