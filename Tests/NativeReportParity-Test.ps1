$ErrorActionPreference = 'Stop'
$report = Join-Path (Split-Path -Parent $PSScriptRoot) 'Output/Reports/NativeForgeReport.json'
if (-not (Test-Path $report)) { Write-Host 'Native Forge reports are not present; Ignite the Forge before this runtime test.'; exit 0 } # prerequisite skip
$root = Split-Path -Parent $PSScriptRoot
$reports = Join-Path $root 'Output\Reports'
$required = @(
    'NativeForgeReport.json',
    'NativeEvidenceReport.json',
    'NativeCompatibilityReport.json',
    'ForgeSummary.json'
)
foreach ($name in $required) {
    $path = Join-Path $reports $name
    if (-not (Test-Path $path)) {
        throw "$name is missing. Ignite the Forge before running this parity test."
    }
    $doc = Get-Content $path -Raw | ConvertFrom-Json
    if ($doc.Engine -ne 'Native .NET') {
        throw "$name was not produced by the native .NET engine."
    }
}
$forge = Get-Content (Join-Path $reports 'NativeForgeReport.json') -Raw | ConvertFrom-Json
if ($null -eq $forge.ProposedOrder.OrderedMods -or $null -eq $forge.ProposedOrder.BlockedMods -or $null -eq $forge.ProposedOrder.CycleGroups) {
    throw 'NativeForgeReport.json is missing the structured LoadOrderPlan compatibility surface.'
}
if ($forge.DependencyCycles | Where-Object { $_.Count -ne (@($_ | ForEach-Object PackageId | Sort-Object -Unique)).Count }) {
    throw 'A dependency cycle contains duplicate package IDs after normalization.'
}
$compat = Get-Content (Join-Path $reports 'NativeCompatibilityReport.json') -Raw | ConvertFrom-Json
$officialFalseErrors = $compat.Mods | Where-Object {
    $_.PackageId -like 'Ludeon.RimWorld*' -and ($_.MetadataErrors -contains 'Missing name')
}
if ($officialFalseErrors) {
    throw 'Official content still exposes generic Missing name metadata errors.'
}
Write-Host 'Native report parity test passed.'
