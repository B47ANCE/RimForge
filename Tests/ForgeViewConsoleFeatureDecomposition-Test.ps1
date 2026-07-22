$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$forge=(Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml') -Raw)+"`n"+(Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml.cs') -Raw)
$console=Get-Content (Join-Path $root 'src\RimForge.App\Features\Console\ConsoleView.xaml') -Raw
foreach($token in @('ForgeGraphCanvas','Graph','Outline')){if(-not $forge.Contains($token)){throw "Missing ForgeView contract: $token"}}
if($console.Length -lt 100){throw 'Console feature view is unexpectedly empty.'}
Write-Host 'ForgeView/Console feature decomposition validation passed.'
