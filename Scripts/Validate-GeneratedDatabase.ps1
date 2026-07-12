param(
    [string]$DatabaseRoot = (
        Join-Path `
            (Split-Path -Parent $PSScriptRoot) `
            "Database.Generated"
    )
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modulePath = Join-Path `
    (Split-Path -Parent $PSScriptRoot) `
    "Modules\DatabaseBuilder.psm1"

Import-Module $modulePath -Force

$result = Test-RimForgeGeneratedDatabase `
    -DatabaseRoot $DatabaseRoot

foreach ($warning in @($result.Warnings)) {
    Write-Warning $warning
}

if (-not $result.IsValid) {
    foreach ($errorMessage in @($result.Errors)) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host (
    "Database validation passed with {0} warning(s)." -f
    $result.WarningCount
)

exit 0
