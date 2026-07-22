$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$interface = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/IModAnalysisEngine.cs') -Raw
if (-not $interface.Contains('targetRimWorldVersion')) { throw 'IModAnalysisEngine does not accept target RimWorld version.' }

$dnaInterface = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/IForgeDnaService.cs') -Raw
if (-not $dnaInterface.Contains('targetRimWorldVersion')) { throw 'IForgeDnaService does not accept target RimWorld version.' }

$main = Get-Content (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs') -Raw
if (($main.Split('TargetRimWorldVersion').Count - 1) -lt 4) { throw 'MainWindow does not consistently pass the selected target version through analysis.' }

$runner = Get-Content (Join-Path $root 'src/RimForge.App/Forge/NativeForgeRunner.cs') -Raw
if ($runner.Contains('TargetRimWorldVersion = "1.6"')) { throw 'NativeForgeRunner still hard-codes RimWorld 1.6.' }
if ($runner.Contains('Contains("1.6"')) { throw 'NativeForgeRunner compatibility checks still hard-code RimWorld 1.6.' }

Write-Host 'Target-version analysis contract passed.' -ForegroundColor Green
