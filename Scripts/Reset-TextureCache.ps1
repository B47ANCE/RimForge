param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),

    [switch]$IncludeInventoryCache
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$cacheFolder = Join-Path $ProjectRoot "Cache\Textures"
$paths = @(
    (Join-Path $cacheFolder "TextureConversionCache.json")
)

if ($IncludeInventoryCache) {
    $paths += Join-Path $cacheFolder "TextureInventory.json"
}

$removed = 0

foreach ($path in @($paths)) {
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Remove-Item -LiteralPath $path -Force
        $removed++
    }
}

Write-Host "Removed $removed texture cache file(s)."
