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

$progress = {
    param($item)

    Write-Progress `
        -Id 60 `
        -Activity "RimForge texture restore" `
        -Status ("[{0}/{1}] {2}" -f
            $item.Current,
            $item.Total,
            $item.Name) `
        -PercentComplete $item.Percent
}

$result = Restore-RimForgeTextures `
    -ManifestPath $ManifestPath `
    -ProgressCallback $progress

Write-Progress -Id 60 -Activity "RimForge texture restore" -Completed
Write-Host (
    "Texture restore complete. Restored={0}, failed={1}" -f
    $result.Restored,
    $result.Failed
)
