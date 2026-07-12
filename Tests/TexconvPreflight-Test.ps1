Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Import-Module `
    (Join-Path $root "Modules\TextureOptimizer.psm1") `
    -Force `
    -ErrorAction Stop

foreach ($name in @(
    "Test-RimForgePortableExecutable",
    "Test-RimForgeTexconvExecutable",
    "Get-RimForgeTexconvStatus",
    "Install-RimForgeTexconv",
    "Resolve-RimForgeTexconvPath"
)) {
    if ($null -eq (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Missing texconv preflight command: $name"
    }
}

Write-Host "RimForge texconv preflight tests passed." -ForegroundColor Green
