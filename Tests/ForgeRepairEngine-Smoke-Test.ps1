$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$planner = Join-Path $root 'src\RimForge.Analysis\Services\RepairPlanner.cs'
$models = Join-Path $root 'src\RimForge.Analysis\Models\RepairModels.cs'
$history = Join-Path $root 'src\RimForge.Analysis\Services\RepairHistoryStore.cs'

foreach ($path in @($planner, $models, $history)) {
    if (-not (Test-Path $path)) { throw "Missing ForgeRepair foundation file: $path" }
}

$text = Get-Content $planner -Raw
foreach ($token in @('BuildCyclePlan', 'AwaitingUserChoice', 'selectedCycleFirstPackageId', 'InstallDependency', 'ReorderProfile')) {
    if ($text -notmatch [regex]::Escape($token)) { throw "Repair planner is missing token: $token" }
}

if ($text -notmatch 'Choose which mod should load first') {
    throw 'Dependency-cycle repair does not require the user to choose the first mod.'
}

Write-Host 'ForgeRepair Engine foundation smoke test passed.'
