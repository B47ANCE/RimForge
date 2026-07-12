Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$modulePath = Join-Path $root "Modules\TextureOptimizer.psm1"
$rulesPath = Join-Path $root "Database\TextureRules.json"

Import-Module $modulePath -Force -ErrorAction Stop
$rules = Import-RimForgeTextureRules -Path $rulesPath

# Only test the public API exported by TextureOptimizer.psm1.
# Internal helper functions are intentionally private module implementation.
$requiredPublicCommands = @(
    "Import-RimForgeTextureRules",
    "Resolve-RimForgeTexconvPath",
    "Get-RimForgeNearestMultiple",
    "Get-RimForgeTextureGeometry",
    "Get-RimForgeTextureInventory",
    "New-RimForgeTextureConversionPlan",
    "Invoke-RimForgeTextureConversion",
    "Test-RimForgeDdsFile",
    "Restore-RimForgeTextures"
)

foreach ($command in @($requiredPublicCommands)) {
    if ($null -eq (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Missing exported texture command: $command"
    }
}

if ([int]$rules.TexconvBatchSize -lt 1) {
    throw "TexconvBatchSize must be positive."
}

if ([string]$rules.InstallStrategy -notin @("Copy","Move","HardLink")) {
    throw "Invalid InstallStrategy."
}

if ([string]$rules.BackupStrategy -notin @("Copy","Move")) {
    throw "Invalid BackupStrategy."
}

if (-not [bool]$rules.UseConversionCache) {
    Write-Warning "UseConversionCache is disabled."
}

if (-not [bool]$rules.SkipValidatedOutputs) {
    Write-Warning "SkipValidatedOutputs is disabled."
}

# Confirm the private performance helpers are present in module source without
# requiring them to be exported into the public API.
$moduleSource = Get-Content -LiteralPath $modulePath -Raw

$requiredPrivateFunctions = @(
    "Get-RimForgeTextureFileSignature",
    "Get-RimForgeTextureConversionCache",
    "New-RimForgeFastPreparedInput",
    "Invoke-RimForgeTexconvBatch",
    "Install-RimForgeConvertedTexture"
)

foreach ($functionName in @($requiredPrivateFunctions)) {
    $pattern = "function\s+{0}\s*\{{" -f [regex]::Escape($functionName)

    if ($moduleSource -notmatch $pattern) {
        throw "Missing private texture performance function in module source: $functionName"
    }
}

Write-Host "RimForge texture performance tests passed." -ForegroundColor Green