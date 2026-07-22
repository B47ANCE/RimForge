$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($file in @('README.md','ROADMAP.md','ARCHITECTURE.md','DESIGN_SYSTEM.md','SORTING_ENGINE.md','RUNTIME_COMPANION.md')){if(-not(Test-Path (Join-Path $root $file))){throw "Missing canonical documentation: $file"}}
$design=Get-Content (Join-Path $root 'DESIGN_SYSTEM.md') -Raw
if($design -notmatch 'Sheriff|badge|anvil'){throw 'Design system does not document current badge/anvil authority.'}
Write-Host 'Documentation contract validation passed.'
