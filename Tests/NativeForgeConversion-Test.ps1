$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$runner=Get-Content (Join-Path $root 'src\RimForge.App\Forge\NativeForgeRunner.cs') -Raw
$composition=Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
$main=Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
foreach($token in @('NativeForgeRunner','NativeForgeReport.json','WriteJsonAtomicAsync')){if(-not $runner.Contains($token)){throw "Native Forge runner missing: $token"}}
if(-not (($composition+$main).Contains('IModAnalysisEngine'))){throw 'Native analysis engine composition is missing.'}
Write-Host 'Native Forge conversion test passed.'
