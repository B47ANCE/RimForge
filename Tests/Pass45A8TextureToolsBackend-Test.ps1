$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$required = @(
  'src\RimForge.App\Features\TextureTools\TextureToolsView.xaml',
  'src\RimForge.App\Features\TextureTools\TextureToolsView.xaml.cs',
  'src\RimForge.App\Features\TextureTools\TextureConversionEngine.cs',
  'src\RimForge.App\Features\TextureTools\TextureConversionModels.cs'
)
foreach ($relative in $required) {
  $path = Join-Path $root $relative
  if (-not (Test-Path $path)) { throw "Missing texture tools file: $relative" }
}
$main = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml') -Raw
if ($main -notmatch 'texturetools:TextureToolsView') { throw 'TextureToolsView is not hosted in MainWindow.' }
if ($main -notmatch 'TextureTools_ActivityRequested') { throw 'Texture Tools activity is not connected to the application console.' }
$engine = Get-Content (Join-Path $root 'src\RimForge.App\Features\TextureTools\TextureConversionEngine.cs') -Raw
foreach ($token in @('ConvertAsync', 'ValidateDds', 'PngBitmapEncoder', 'JpegBitmapEncoder', 'texconv.exe')) {
  if ($engine -notmatch [regex]::Escape($token)) { throw "Texture backend missing: $token" }
}
$forge = Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeViewView.xaml') -Raw
if ($forge -notmatch '<RowDefinition Height="\*"/>\s*<RowDefinition Height="Auto"/>') { throw 'ForgeView map/context row layout not found.' }
if ($forge -notmatch 'Grid.Row="1" Margin="0,12,0,0"') { throw 'Selection Context is not below the map.' }
Write-Host 'Pass45A8TextureToolsBackend-Test: PASSED' -ForegroundColor Green
