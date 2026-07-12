param(
    [Parameter(Mandatory)]
    [string]$ManifestPath,

    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module `
    (Join-Path $ProjectRoot "Modules\TextureOptimizer.psm1") `
    -Force `
    -ErrorAction Stop

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$errors = @()

foreach ($item in @($manifest.Items)) {
    if ($item.Status -notin @("Staged","Installed")) {
        continue
    }

    $path = if ($item.Status -eq "Installed") {
        [string]$item.InstallPath
    } else {
        [string]$item.StagePath
    }

    $result = Test-RimForgeDdsFile `
        -Path $path `
        -ExpectedWidth ([int]$item.CanvasWidth) `
        -ExpectedHeight ([int]$item.CanvasHeight) `
        -ExpectMipmaps $true

    if (-not $result.IsValid) {
        $errors += "{0}: {1}" -f $path, $result.Error
    }
}

if (@($errors).Count -gt 0) {
    foreach ($errorMessage in @($errors)) {
        Write-Error $errorMessage
    }

    throw "Texture validation failed for $(@($errors).Count) file(s)."
}

Write-Host "Texture manifest validation passed."
