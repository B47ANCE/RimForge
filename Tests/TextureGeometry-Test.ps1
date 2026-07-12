Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Import-Module `
    (Join-Path $root "Modules\TextureOptimizer.psm1") `
    -Force

$cases = @(
    @{ W = 513; H = 301; CW = 512; CH = 300 },
    @{ W = 512; H = 300; CW = 512; CH = 300 },
    @{ W = 6; H = 6; CW = 8; CH = 8 },
    @{ W = 5; H = 7; CW = 4; CH = 8 }
)

foreach ($case in @($cases)) {
    $geometry = Get-RimForgeTextureGeometry `
        -OriginalWidth $case.W `
        -OriginalHeight $case.H `
        -DimensionMultiple 4 `
        -TieRule Up

    if (
        $geometry.CanvasWidth -ne $case.CW -or
        $geometry.CanvasHeight -ne $case.CH
    ) {
        throw (
            "Geometry test failed for {0}x{1}: got {2}x{3}" -f
            $case.W, $case.H,
            $geometry.CanvasWidth, $geometry.CanvasHeight
        )
    }

    if (
        ($geometry.CanvasWidth % 4) -ne 0 -or
        ($geometry.CanvasHeight % 4) -ne 0
    ) {
        throw "Canvas dimensions are not divisible by four."
    }

    if (
        $geometry.ScaledWidth -gt $geometry.CanvasWidth -or
        $geometry.ScaledHeight -gt $geometry.CanvasHeight
    ) {
        throw "Scaled image does not fit inside canvas."
    }
}

Write-Host "RimForge texture geometry tests passed." -ForegroundColor Green
