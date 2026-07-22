$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$servicePath = Join-Path $repo 'src\RimForge.Infrastructure\Services\ForgeEvidenceService.cs'
$optionsPath = Join-Path $repo 'src\RimForge.Infrastructure\Services\ForgeEvidenceRefreshInfrastructure.cs'
$appPath = Join-Path $repo 'src\RimForge.App\MainWindow.xaml.cs'
$sharedPath = Join-Path $repo 'src\RimForge.App\Features\SharedEvidence\MainWindow.SharedEvidence.cs'

foreach ($path in @($servicePath, $optionsPath, $appPath, $sharedPath)) {
    if (-not (Test-Path $path)) { throw "Missing required file: $path" }
}

$service = Get-Content $servicePath -Raw
$options = Get-Content $optionsPath -Raw
$app = Get-Content $appPath -Raw
$shared = Get-Content $sharedPath -Raw

$requiredService = @(
    'ConcurrentDictionary<string, Lazy<Task<ForgeEvidenceSnapshot>>>',
    'LazyThreadSafetyMode.ExecutionAndPublication',
    'BuildRequestSignature',
    'ReferenceEquals(candidate, scheduled)',
    'refresh.WaitAsync(cancellationToken)',
    'SemaphoreSlim _schedulerGate',
    'QueueWatcherInvalidation',
    'DebounceInvalidationAsync',
    'ForgeEvidenceInvalidationReason.WatcherOverflow',
    'Interlocked.Increment(ref _watcherOverflows)',
    'CancelCurrent()',
    '_activeScanCts?.Cancel()',
    'int CoalescedRequests',
    'int DebouncedInvalidations',
    'int WatcherOverflows'
)
foreach ($token in $requiredService) {
    if ($service -notmatch [regex]::Escape($token)) { throw "Scheduler/watcher contract missing: $token" }
}

if ($options -notmatch [regex]::Escape('TimeSpan.FromMilliseconds(400)')) {
    throw 'Scheduler/watcher contract missing: TimeSpan.FromMilliseconds(400)'
}

if ($service -match 'FileSystemEventHandler changed = \(_, _\) => Invalidate\(') {
    throw 'File watcher callbacks still invalidate immediately instead of using debounce.'
}
if ($service -match 'RunScheduledRefreshAsync\([^\)]*cancellationToken') {
    throw 'Caller cancellation is still directly coupled to the shared generation.'
}
if ($service -match 'ContinueWith\(\s*\(\s*_\s*,\s*state\s*\)\s*=>') {
    throw 'Continuation task parameter uses underscore and can capture out _ instead of a discard.'
}
if ($service -notmatch 'ContinueWith\(\s*\(\s*completedTask\s*,\s*state\s*\)\s*=>') {
    throw 'Continuation cleanup lambda does not use an explicit task parameter name.'
}

foreach ($token in @(
    'snapshot.Metrics.CoalescedRequests',
    'snapshot.Metrics.DebouncedInvalidations',
    'snapshot.Metrics.WatcherOverflows'
)) {
    if ($app -notmatch [regex]::Escape($token)) { throw "Reliability metrics report missing: $token" }
}

if ($shared -notmatch [regex]::Escape('_forgeEvidenceSnapshot.Metrics.CoalescedRequests')) {
    throw 'Shared evidence status does not expose scheduler coalescing.'
}

Write-Host 'Pass46A4SchedulerWatcherReliability-Test: PASSED' -ForegroundColor Green
