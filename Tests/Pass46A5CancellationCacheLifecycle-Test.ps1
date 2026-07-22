$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$servicePath = Join-Path $repo 'src\RimForge.Infrastructure\Services\ForgeEvidenceService.cs'
$scannerPath = Join-Path $repo 'src\RimForge.Infrastructure\Services\ModEvidenceScanner.cs'
$appPath = Join-Path $repo 'src\RimForge.App\MainWindow.xaml.cs'

foreach ($path in @($servicePath, $scannerPath, $appPath)) {
    if (-not (Test-Path $path)) { throw "Missing required file: $path" }
}

$service = Get-Content $servicePath -Raw
$scanner = Get-Content $scannerPath -Raw
$app = Get-Content $appPath -Raw

foreach ($token in @(
    'scanCts.Token.ThrowIfCancellationRequested()',
    'Publication and CancelCurrent share this gate',
    'lock (_stateGate)',
    'CleanupCacheAsync(',
    'cleanup.CacheFilesDeleted',
    'cleanup.TemporaryFilesDeleted',
    'cleanup.QuarantineFilesDeleted',
    'int CacheFilesDeleted',
    'int TemporaryFilesDeleted',
    'int QuarantineFilesDeleted'
)) {
    if ($service -notmatch [regex]::Escape($token)) { throw "Cancellation/publication contract missing: $token" }
}

$publicationPattern = 'lock\s*\(_stateGate\)\s*\{[^}]*scanCts\.Token\.ThrowIfCancellationRequested\(\);[^}]*_bus\.Publish\(snapshot, ForgeEvidencePublicationReason\.Refreshed\);'
if ($service -notmatch $publicationPattern) {
    throw 'Snapshot publication is not protected by a cancellation check under the shared state gate.'
}

foreach ($token in @(
    'ModEvidenceCacheCleanupResult',
    'TimeSpan.FromHours(24)',
    'TimeSpan.FromDays(7)',
    'name.EndsWith(versionSuffix',
    '!activeCacheNames.Contains(name)',
    'Cache maintenance is best-effort and must never block publication.'
)) {
    if ($scanner -notmatch [regex]::Escape($token)) { throw "Cache lifecycle contract missing: $token" }
}

foreach ($token in @(
    'snapshot.Metrics.CacheFilesDeleted',
    'snapshot.Metrics.TemporaryFilesDeleted',
    'snapshot.Metrics.QuarantineFilesDeleted'
)) {
    if ($app -notmatch [regex]::Escape($token)) { throw "Cache lifecycle metric report missing: $token" }
}

Write-Host 'Pass46A5CancellationCacheLifecycle-Test: PASSED' -ForegroundColor Green
