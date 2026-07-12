[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$config = Get-Content -LiteralPath (Join-Path $root 'Config.json') -Raw | ConvertFrom-Json
$statePath = Join-Path (Join-Path $root ([string]$config.CacheFolder)) 'Incremental\ModState.json'
if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
    Write-Host 'No RimForge incremental baseline exists yet.' -ForegroundColor Yellow
    exit 0
}
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
[PSCustomObject]@{
    WrittenUtc = $state.WrittenUtc
    TrackedMods = @($state.Mods).Count
    LastChanged = if ($null -ne $state.LastRun) { $state.LastRun.ChangedMods } else { $null }
    LastUnchanged = if ($null -ne $state.LastRun) { $state.LastRun.UnchangedMods } else { $null }
    LastTotalSeconds = if ($null -ne $state.LastRun -and $null -ne $state.LastRun.Timing) { [math]::Round(([double]$state.LastRun.Timing.TotalMilliseconds / 1000),2) } else { $null }
    StatePath = $statePath
} | Format-List
