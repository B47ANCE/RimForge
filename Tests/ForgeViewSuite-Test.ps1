$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$text=(Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeGraphCanvas.cs') -Raw)+"`n"+(Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml.cs') -Raw)
foreach($token in @('MouseWheel','Zoom','Pan','Selected')){if($text -notmatch [regex]::Escape($token)){throw "Missing ForgeView behavior: $token"}}
Write-Host 'ForgeView suite validation passed.'
