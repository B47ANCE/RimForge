param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "_TextureBootstrap.ps1")

$context = Initialize-RimForgeTextureContext -ProjectRoot $ProjectRoot
$progress = New-RimForgeConsoleProgressCallback

$inventory = Get-RimForgeInvalidDdsInventory `
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
            "InvalidDdsRepairPlan.json"
    )

$plan = New-RimForgeInvalidDdsRepairPlan `
    -Inventory @($inventory) `
    -Rules $context.Rules `
    -ProjectRoot $ProjectRoot `
    -OutputPath $planPath

Write-Progress `
    -Id 60 `
    -Activity "RimForge DDS header scan" `
    -Completed

$invalidCount = @(
    $inventory |
    Where-Object {
        $_.PSObject.Properties.Name -contains "Status" -and
        $_.Status -eq "InvalidDimensions"
    }
).Count

$missingPngCount = @(
    $inventory |
    Where-Object {
        $_.PSObject.Properties.Name -contains "Status" -and
        $_.Status -eq "MissingSourcePng"
    }
).Count

$unreadableDdsCount = @(
    $inventory |
    Where-Object {
        $_.PSObject.Properties.Name -contains "Status" -and
        $_.Status -eq "UnreadableDds"
    }
).Count

Write-Host (
    "Invalid DDS analysis complete. Invalid dimensions={0}, scheduled={1}, missing PNG={2}, unreadable DDS={3}" -f
    $invalidCount,
    $plan.Summary.Scheduled,
    $missingPngCount,
    $unreadableDdsCount
)

Write-Host "Repair plan: $planPath"
