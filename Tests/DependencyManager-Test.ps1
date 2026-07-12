Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $root 'Modules\TextureOptimizer.psm1') -Force -DisableNameChecking -ErrorAction Stop
Import-Module (Join-Path $root 'Modules\DependencyManager.psm1') -Force -DisableNameChecking -ErrorAction Stop
$manifest = Import-RimForgeDependencyManifest -Path (Join-Path $root 'Database\Dependencies.json')
if (@($manifest.Dependencies).Count -lt 1) { throw 'No dependencies were loaded.' }
$texconv = @($manifest.Dependencies | Where-Object Id -eq 'texconv')
if ($texconv.Count -ne 1) { throw 'Texconv dependency definition is missing or duplicated.' }
$status = Test-RimForgeDependency -ProjectRoot $root -Dependency $texconv[0]
foreach ($name in @('Id','IsInstalled','IsValid','CanInstall','Error')) {
    if (-not ($status.PSObject.Properties.Name -contains $name)) { throw "Dependency status is missing '$name'." }
}
Write-Host 'RimForge dependency manager tests passed.' -ForegroundColor Green
