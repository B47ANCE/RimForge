param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),

    [switch]$InstallIfMissing,

    [switch]$NoInstallPrompt
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module `
    (Join-Path $ProjectRoot "Modules\TextureOptimizer.psm1") `
    -Force `
    -ErrorAction Stop

$rules = Import-RimForgeTextureRules `
    -Path (Join-Path $ProjectRoot "Database\TextureRules.json")

$status = Get-RimForgeTexconvStatus `
    -ProjectRoot $ProjectRoot `
    -ConfiguredPath ([string]$rules.ToolPath)

if (-not $status.IsValid -and $status.CanInstall) {
    $approved = $InstallIfMissing

    if (-not $approved -and -not $NoInstallPrompt) {
        $approved = Request-RimForgeTexconvInstallApproval `
            -Reason (
                "texconv is missing or invalid.`r`n" +
                "Detected path: $($status.Path)`r`n" +
                "Reason: $($status.Error)"
            )
    }

    if ($approved) {
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

        $status = Get-RimForgeTexconvStatus `
            -ProjectRoot $ProjectRoot `
            -ConfiguredPath ([string]$rules.ToolPath)
    }
}

$status | Format-List

if (-not $status.IsValid) {
    exit 1
}

exit 0
