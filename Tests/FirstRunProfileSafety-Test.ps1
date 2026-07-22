$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$text=(Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ProfileWorkspaceService.cs') -Raw)+"`n"+(Get-Content (Join-Path $root 'src\RimForge.Core\Models\LoadOrderRules.cs') -Raw)
if($text -notmatch 'Normalize'){throw 'Starter profile path is not normalized through canonical load-order rules.'}
if($text -notmatch 'ludeon\.rimworld'){throw 'Starter profile safety does not identify RimWorld Core.'}
Write-Host 'First-run profile safety validation passed.'
