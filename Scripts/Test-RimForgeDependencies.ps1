[CmdletBinding()]
param(
    [switch]$InstallMissing,
    [switch]$AcceptPackageAgreements
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Import-Module (Join-Path $root 'Modules\TextureOptimizer.psm1') -Force -DisableNameChecking -ErrorAction Stop
Import-Module (Join-Path $root 'Modules\DependencyManager.psm1') -Force -DisableNameChecking -ErrorAction Stop

$config = Get-Content -LiteralPath (Join-Path $root 'Config.json') -Raw | ConvertFrom-Json
$manifestPath = if ($config.PSObject.Properties.Name -contains 'DependencyManifest') {
    Join-Path $root ([string]$config.DependencyManifest)
} else {
    Join-Path $root 'Database\Dependencies.json'
}
$manifest = Import-RimForgeDependencyManifest -Path $manifestPath

$statuses = @()
foreach ($dependency in @($manifest.Dependencies)) {
    $status = if ($InstallMissing) {
        Ensure-RimForgeDependency -ProjectRoot $root -Dependency $dependency -TimeoutSeconds ([int]$config.ExternalTimeoutSeconds) -PromptForInstall -AcceptPackageAgreements:$AcceptPackageAgreements
    } else {
        Test-RimForgeDependency -ProjectRoot $root -Dependency $dependency -TimeoutSeconds ([int]$config.ExternalTimeoutSeconds)
    }
    $statuses += $status
}

$statuses | Format-Table Id,Capability,IsInstalled,IsValid,Architecture,Source,Path,Error -AutoSize
if (@($statuses | Where-Object { $_.Required -and -not $_.IsValid }).Count -gt 0) { exit 2 }
if (@($statuses | Where-Object { -not $_.IsValid }).Count -gt 0) { exit 1 }
exit 0
