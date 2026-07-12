Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $root 'Modules\IncrementalAudit.psm1') -Force -DisableNameChecking
$initial = @([PSCustomObject]@{Key='a';Fingerprint='1'},[PSCustomObject]@{Key='b';Fingerprint='2'})
$first = Compare-RimForgeModState -CurrentFingerprints $initial -PreviousState $null
if (-not $first.IsInitialRun -or $first.ChangedCount -ne 2) { throw 'Initial comparison failed.' }
$previous = [PSCustomObject]@{Mods=$initial}
$current = @([PSCustomObject]@{Key='a';Fingerprint='1'},[PSCustomObject]@{Key='b';Fingerprint='3'},[PSCustomObject]@{Key='c';Fingerprint='4'})
$next = Compare-RimForgeModState -CurrentFingerprints $current -PreviousState $previous
if ($next.UnchangedCount -ne 1 -or $next.ChangedCount -ne 2 -or $next.AddedCount -ne 1) { throw 'Incremental comparison counts were incorrect.' }
$session = New-RimForgeTimingSession
Start-RimForgeTimingStage -Session $session -Name 'Test'
Start-Sleep -Milliseconds 10
[void](Stop-RimForgeTimingStage -Session $session -Name 'Test')
$timing = Complete-RimForgeTimingSession -Session $session
if ($timing.Stages.Test -le 0) { throw 'Timing stage was not recorded.' }
Write-Host 'RimForge incremental audit tests passed.' -ForegroundColor Green
