$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$app = Get-Content (Join-Path $root 'src\RimForge.App\App.xaml') -Raw
$main = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml') -Raw
if ($app -notmatch 'x:Key="PersistentForgeScrollBarStyle"') { throw 'PersistentForgeScrollBarStyle is not defined at application scope.' }
if ($main -match 'x:Key="PersistentForgeScrollBarStyle"') { throw 'PersistentForgeScrollBarStyle is still defined in MainWindow scope.' }
Write-Host 'Global scrollbar resource scope validation passed.'
