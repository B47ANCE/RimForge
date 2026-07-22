$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$model = Get-Content (Join-Path $root 'src\RimForge.Core\Models\ExternalProfileReconciliation.cs') -Raw
$service = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ExternalProfileReconciliationService.cs') -Raw
$ui = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.ExternalProfileChanges.cs') -Raw
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
foreach ($check in @(
    @{Text=$model; Token='ExternalProfileReconciliation'; Message='Reconciliation model is missing.'},
    @{Text=$service; Token='XDocument.LoadAsync'; Message='External ModsConfig parser is missing.'},
    @{Text=$service; Token='ProfileOrderChange'; Message='Order-difference projection is missing.'},
    @{Text=$ui; Token='accept-external-profile'; Message='Use External action is missing.'},
    @{Text=$ui; Token='restore-rimforge-profile'; Message='Restore RimForge action is missing.'},
    @{Text=$ui; Token='AcknowledgeCurrentAsync'; Message='Self-write acknowledgement is missing.'},
    @{Text=$composition; Token='ExternalProfileReconciliationService'; Message='Reconciliation service composition is missing.'}
)) {
    if (-not $check.Text.Contains($check.Token)) { throw $check.Message }
}
Write-Host 'External profile reconciliation validation passed.'
