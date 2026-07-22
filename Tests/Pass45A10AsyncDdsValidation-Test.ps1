$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$code = Get-Content (Join-Path $root 'src\RimForge.App\Features\TextureTools\TextureToolsView.xaml.cs') -Raw
$xaml = Get-Content (Join-Path $root 'src\RimForge.App\Features\TextureTools\TextureToolsView.xaml') -Raw
foreach ($token in @(
    'private async void AnalyzeProfile_Click',
    'DiscoverProfileTextures',
    'RunFeatureTaskAsync',
    'Re-Analyze Active Profile',
    'TextureConversionEngine.ValidateDds'
)) {
    if (-not $code.Contains($token)) { throw "Missing integrated profile texture analysis token: $token" }
}
if ($code.Contains('Task.Run')) { throw 'Texture analysis still owns isolated Task.Run execution.' }
if ($code.Contains('CancellationTokenSource')) { throw 'Texture analysis still owns a private cancellation source.' }
foreach ($removed in @('Content="Add Folder"','Content="Validate DDS"','Content="Restore Backups"','Create backups before conversion')) {
    if ($xaml.Contains($removed)) { throw "Legacy Texture Tools control remains: $removed" }
}
Write-Host 'Pass45A10AsyncDdsValidation-Test: PASSED' -ForegroundColor Green
