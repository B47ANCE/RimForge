$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$xamlPath = Join-Path $repoRoot 'src\RimForge.App\Features\TextureTools\TextureToolsView.xaml'
$codePath = Join-Path $repoRoot 'src\RimForge.App\Features\TextureTools\TextureToolsView.xaml.cs'
if (-not (Test-Path $xamlPath)) { throw "Missing TextureToolsView.xaml" }
if (-not (Test-Path $codePath)) { throw "Missing TextureToolsView.xaml.cs" }
$xaml = Get-Content $xamlPath -Raw
$code = Get-Content $codePath -Raw
if ($code -notmatch 'public\s+double\s+ProgressValue\s*\{\s*get\s*=>[^}]+private\s+set') {
    throw 'ProgressValue is expected to remain a read-only public binding source.'
}
$expected = 'Value="{Binding ProgressValue, Mode=OneWay, RelativeSource={RelativeSource AncestorType=UserControl}}"'
if (-not $xaml.Contains($expected)) {
    throw 'Texture Tools progress binding must explicitly use Mode=OneWay to avoid WPF startup failure.'
}
Write-Host 'Pass45A9TextureToolsBootBinding-Test: PASSED' -ForegroundColor Green
