Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$modulePath = Join-Path $root "Modules\TextureOptimizer.psm1"

Import-Module $modulePath -Force -ErrorAction Stop

foreach ($name in @(
    "Request-RimForgeTexconvInstallApproval",
    "Get-RimForgeTexconvStatus",
    "Resolve-RimForgeTexconvPath"
)) {
    if ($null -eq (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Missing texconv UI/preflight command: $name"
    }
}

$moduleSource = Get-Content -LiteralPath $modulePath -Raw

foreach ($privateName in @(
    "ConvertTo-RimForgeWindowsCommandLineArgument",
    "Invoke-RimForgeNativeProcess",
    "Invoke-RimForgeTexconvBatch"
)) {
    if ($moduleSource -notmatch (
        "function\s+{0}\s*\{{" -f [regex]::Escape($privateName)
    )) {
        throw "Missing native process helper: $privateName"
    }
}

Write-Host "RimForge texconv prompt/path tests passed." -ForegroundColor Green
