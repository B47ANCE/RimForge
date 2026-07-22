$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$scannerPath = Join-Path $repo 'src\RimForge.Infrastructure\Services\ModEvidenceScanner.cs'
$servicePath = Join-Path $repo 'src\RimForge.Infrastructure\Services\ForgeEvidenceService.cs'
$appPath = Join-Path $repo 'src\RimForge.App\MainWindow.xaml.cs'

foreach ($path in @($scannerPath, $servicePath, $appPath)) {
    if (-not (Test-Path $path)) { throw "Missing required file: $path" }
}

$scanner = Get-Content $scannerPath -Raw
$service = Get-Content $servicePath -Raw
$app = Get-Content $appPath -Raw

$requiredScanner = @(
    'CacheSchemaVersion',
    'BuildQuickFingerprint',
    'ModEvidenceCacheStatus',
    'FingerprintMismatch',
    'SchemaMismatch',
    'Corrupt',
    'QuarantineCorruptCache',
    'File.Move(tempPath, cachePath, true)',
    'FileOptions.WriteThrough',
    'FlushAsync',
    'finally',
    'TargetRimWorldVersion'
)
foreach ($token in $requiredScanner) {
    if ($scanner -notmatch [regex]::Escape($token)) { throw "Persistent cache contract missing: $token" }
}

$requiredService = @(
    'string Fingerprint',
    'int CacheMisses',
    'int CorruptRecovered',
    'result.Fingerprint',
    'result.CacheStatus == ModEvidenceCacheStatus.Corrupt',
    'Path.GetFullPath(mod.RootPath)',
    'removed'
)
foreach ($token in $requiredService) {
    if ($service -notmatch [regex]::Escape($token)) { throw "Incremental evidence contract missing: $token" }
}

if ($service -match 'previous\.TryGet\(mod\.Id') {
    throw 'Refresh still bypasses fingerprint validation through blind previous-generation reuse.'
}

foreach ($token in @('snapshot.Metrics.CacheMisses', 'snapshot.Metrics.CorruptRecovered')) {
    if ($app -notmatch [regex]::Escape($token)) { throw "Metrics report missing: $token" }
}

Write-Host 'Pass46A3PersistentEvidenceCache-Test: PASSED' -ForegroundColor Green
