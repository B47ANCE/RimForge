param(
    [ValidateSet("Stage","Install")]
    [string]$Mode = "Stage",

    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),

    [string]$PlanPath,

    [switch]$InstallTexconvIfMissing,

    [switch]$NoInstallPrompt
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "_TextureBootstrap.ps1")

$context = Initialize-RimForgeTextureContext -ProjectRoot $ProjectRoot
$progress = New-RimForgeConsoleProgressCallback

$texconvStatus = Get-RimForgeTexconvStatus `
    -ProjectRoot $ProjectRoot `
    -ConfiguredPath ([string]$context.Rules.ToolPath)

if (-not $texconvStatus.IsValid) {
    Write-Warning (
        "texconv preflight failed. Path={0}; Reason={1}" -f
        $texconvStatus.Path,
        $texconvStatus.Error
    )

    $approved = $false

    if ($InstallTexconvIfMissing) {
        $approved = $true
    }
    elseif (-not $NoInstallPrompt -and $texconvStatus.CanInstall) {
        $approved = Request-RimForgeTexconvInstallApproval `
            -Reason (
                "texconv is missing or invalid.`r`n" +
                "Detected path: $($texconvStatus.Path)`r`n" +
                "Reason: $($texconvStatus.Error)"
            )
    }

    if ($approved -and $texconvStatus.CanInstall) {
        Write-Host "Installing Microsoft.DirectXTex.Texconv with Winget..."

        Install-RimForgeTexconv `
            -AcceptPackageAgreements `
            -Confirm:$false

        $env:PATH = (
            [System.Environment]::GetEnvironmentVariable(
                "PATH",
                [System.EnvironmentVariableTarget]::Machine
            ) +
            ";" +
            [System.Environment]::GetEnvironmentVariable(
                "PATH",
                [System.EnvironmentVariableTarget]::User
            )
        )

        $texconvStatus = Get-RimForgeTexconvStatus `
            -ProjectRoot $ProjectRoot `
            -ConfiguredPath ([string]$context.Rules.ToolPath)
    }

    if (-not $texconvStatus.IsValid) {
        throw (
            "Texture conversion cannot start because texconv is missing or " +
            "invalid. Installation was declined, unavailable, or unsuccessful."
        )
    }
}

Write-Host (
    "texconv ready: {0} ({1}, source={2})" -f
    $texconvStatus.Path,
    $texconvStatus.Architecture,
    $texconvStatus.Source
)

if ([string]::IsNullOrWhiteSpace($PlanPath)) {
    $PlanPath = Resolve-RimForgeTexturePath `
        -ProjectRoot $ProjectRoot `
        -Path (
            Join-Path `
                ([string]$context.Rules.PlanFolder) `
                "TexturePlan.json"
        )
}

if (-not (Test-Path -LiteralPath $PlanPath -PathType Leaf)) {
    throw "Texture plan not found. Run Analyze-ModTextures.ps1 first: $PlanPath"
}

$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json

$manifestName = "Texture-{0}-{1}.json" -f
    $Mode,
    (Get-Date -Format "yyyyMMdd-HHmmss")

$manifestPath = Resolve-RimForgeTexturePath `
    -ProjectRoot $ProjectRoot `
    -Path (
        Join-Path `
            ([string]$context.Rules.ManifestFolder) `
            $manifestName
    )

$manifest = Invoke-RimForgeTextureConversion `
    -Plan $plan `
    -Rules $context.Rules `
    -ProjectRoot $ProjectRoot `
    -Mode $Mode `
    -TexconvPath ([string]$texconvStatus.Path) `
    -ManifestPath $manifestPath `
    -ProgressCallback $progress

Write-Progress -Id 60 -Activity "RimForge texture conversion" -Completed

Write-Host (
    "Texture conversion complete. Staged={0}, installed={1}, cached={2}, skipped={3}, failed={4}" -f
    $manifest.Summary.Staged,
    $manifest.Summary.Installed,
    $manifest.Summary.Cached,
    $manifest.Summary.Skipped,
    $manifest.Summary.Failed
)
Write-Host "Manifest: $manifestPath"
