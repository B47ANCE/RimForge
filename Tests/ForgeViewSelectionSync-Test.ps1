$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$text=(Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml') -Raw)+"`n"+(Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml') -Raw)
foreach($token in @('SelectedPackageId','Selection','FOCUSED LINKS')){if(-not $text.Contains($token)){throw "Missing ForgeView selection presentation: $token"}}
Write-Host 'ForgeView selection synchronization validation passed.'
