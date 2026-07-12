param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "_TextureBootstrap.ps1")

$context = Initialize-RimForgeTextureContext -ProjectRoot $ProjectRoot
$progress = New-RimForgeConsoleProgressCallback

$inventory = Get-RimForgeTextureInventory `
    -Mods @($context.Mods) `
    -Rules $context.Rules `
    -ProjectRoot $ProjectRoot `
    -TargetVersion ([string]$context.Rules.TargetVersion) `
    -ProgressCallback $progress

$planPath = Resolve-RimForgeTexturePath `
    -ProjectRoot $ProjectRoot `
    -Path (
        Join-Path `
            ([string]$context.Rules.PlanFolder) `
            "TexturePlan.json"
    )

$plan = New-RimForgeTextureConversionPlan `
    -Inventory @($inventory) `
    -Rules $context.Rules `
    -ProjectRoot $ProjectRoot `
    -OutputPath $planPath

Write-Progress -Id 60 -Activity "RimForge texture analysis" -Completed

$ready = @(
    $plan.Items |
    Where-Object {
        $_.PSObject.Properties.Name -contains "Disposition" -and
        [string]$_.Disposition -eq "Convert"
    }
).Count

$existing = @(
    $plan.Items |
    Where-Object {
        $_.PSObject.Properties.Name -contains "Disposition" -and
        [string]$_.Disposition -eq "SkipExistingDds"
    }
).Count

$unreadable = @(
    $plan.Items |
    Where-Object {
        $_.PSObject.Properties.Name -contains "Status" -and
        [string]$_.Status -eq "Unreadable"
    }
).Count

Write-Host (
    "Texture analysis complete. Convert={0}, existing DDS skipped={1}, unreadable={2}" -f
    $ready, $existing, $unreadable
)
Write-Host "Plan: $planPath"