$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$service = Get-Content (Join-Path $repo 'src/RimForge.Infrastructure/Services/ForgeEvidenceService.cs') -Raw
$infra = Get-Content (Join-Path $repo 'src/RimForge.Infrastructure/Services/ForgeEvidenceRefreshInfrastructure.cs') -Raw
$scheduler = Get-Content (Join-Path $repo 'src/RimForge.Infrastructure/Services/ForgeEvidenceRefreshScheduler.cs') -Raw
$composition = Get-Content (Join-Path $repo 'src/RimForge.App/Composition/RimForgeApplicationServices.cs') -Raw
$window = Get-Content (Join-Path $repo 'src/RimForge.App/MainWindow.xaml.cs') -Raw
$docs = Get-Content (Join-Path $repo 'docs/architecture/FORGE_EVIDENCE_INCREMENTAL_REFRESH.md') -Raw

@(
    'ForgeEvidenceInvalidationJournal',
    'Interlocked.Increment(ref _sequence)',
    'current.Sequence == invalidation.Sequence',
    'ForgeEvidenceContributionReconciler',
    'refreshedSources.Contains',
    'ForgeEvidenceWatcherFilter',
    'RelevantExtensions',
    'IgnoreTransientFiles'
) | ForEach-Object { if (-not $infra.Contains($_)) { throw "Missing incremental refresh infrastructure: $_" } }

@(
    '_invalidationJournal.Capture()',
    '_invalidationJournal.Acknowledge(capturedInvalidations)',
    'invalidatedRootPaths.Contains',
    'ForgeEvidenceContributionReconciler.ReconcileRefresh',
    '_options.MaximumParallelScans',
    '_options.WatcherDebounce',
    '_options.WatcherBufferSize',
    'PendingInvalidations = 0',
    'ReconciledContributions = 0',
    'ActiveWatchers = 0'
) | ForEach-Object { if (-not $service.Contains($_)) { throw "Missing incremental service behavior: $_" } }

@(
    'IForgeEvidenceRefreshScheduler',
    'ForgeEvidenceRefreshRequest',
    'InvalidationSettleDelay',
    '_service.Invalidated += OnInvalidated',
    'RefreshAfterSettleAsync',
    '_service.RefreshAsync'
) | ForEach-Object { if (-not $scheduler.Contains($_)) { throw "Missing refresh scheduler behavior: $_" } }

if (-not $composition.Contains('ForgeEvidenceRefreshScheduler = forgeEvidenceRefreshScheduler')) {
    throw 'Refresh scheduler is not part of application composition.'
}
if (-not $composition.Contains('await ForgeEvidenceRefreshScheduler.DisposeAsync()')) {
    throw 'Refresh scheduler is not disposed before the evidence service.'
}
if (-not $window.Contains('_forgeEvidenceRefreshScheduler.Configure')) {
    throw 'The application does not configure the installed-library refresh request.'
}
if (-not $window.Contains('_forgeEvidenceRefreshScheduler.Start()')) {
    throw 'Automatic evidence refresh is not enabled with shared intelligence.'
}
if (-not $docs.Contains('monotonic sequence')) { throw 'Incremental refresh documentation is incomplete.' }
Write-Host 'Epic1ForgeEvidenceIncrementalRefresh-Test: PASS'
