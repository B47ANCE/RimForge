$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$xaml = Get-Content (Join-Path $root 'src\RimForge.App\Features\TextureTools\TextureToolsView.xaml') -Raw
$view = Get-Content (Join-Path $root 'src\RimForge.App\Features\TextureTools\TextureToolsView.xaml.cs') -Raw
$engine = Get-Content (Join-Path $root 'src\RimForge.App\Features\TextureTools\TextureConversionEngine.cs') -Raw
$manifest = Get-Content (Join-Path $root 'src\RimForge.App\Features\TextureTools\TextureConversionManifestStore.cs') -Raw
foreach ($token in @('AnalyzeButtonText','Convert Selected','Convert All Eligible','Revert Conversion','Mode=OneWay')) {
    if (-not $xaml.Contains($token) -and -not $view.Contains($token)) { throw "Missing integrated Texture Tools token: $token" }
}
foreach ($token in @('NearestMultipleOfFour','value - lower < upper - value ? lower : upper','CreateNormalizedPng','BC3_UNORM','BC1_UNORM')) {
    if (-not $engine.Contains($token)) { throw "Missing nearest-multiple-of-four DDS conversion token: $token" }
}
foreach ($token in @('.rimforge-texture-conversions.json','RecordAsync','RevertAsync','OutputLength')) {
    if (-not $manifest.Contains($token)) { throw "Missing conversion manifest token: $token" }
}
if ($engine.Contains('CreateBackups')) { throw 'Backup conversion behavior must be removed.' }
Write-Host 'Pass45A11TextureToolsIntegratedWorkflow-Test: PASSED' -ForegroundColor Green
