[CmdletBinding(SupportsShouldProcess,ConfirmImpact='Medium')]
param([switch]$IncludeEvidenceCache)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$config = Get-Content -LiteralPath (Join-Path $root 'Config.json') -Raw | ConvertFrom-Json
$cacheRoot = Join-Path $root ([string]$config.CacheFolder)
$targets = @((Join-Path $cacheRoot 'Incremental'),(Join-Path $cacheRoot 'AboutMetadata'))
if ($IncludeEvidenceCache) { $targets += (Join-Path $cacheRoot 'Evidence') }
foreach ($target in $targets) {
    if ((Test-Path -LiteralPath $target) -and $PSCmdlet.ShouldProcess($target,'Remove RimForge incremental cache')) {
        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host "Removed $target"
    }
}
