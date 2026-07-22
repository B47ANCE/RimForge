$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$mainPath = Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs'
$consumerPath = Join-Path $root 'src/RimForge.App/Features/SharedEvidence/MainWindow.SharedEvidence.cs'
$issuePath = Join-Path $root 'src/RimForge.App/Features/IssueViewer/MainWindow.IssueViewer.cs'
$launchPath = Join-Path $root 'src/RimForge.App/Features/LaunchBar/MainWindow.LaunchBar.cs'

foreach ($path in @($mainPath, $consumerPath, $issuePath, $launchPath)) {
    if (-not (Test-Path $path)) { throw "Required consumer migration file is missing: $path" }
}

$main = Get-Content $mainPath -Raw
$consumer = Get-Content $consumerPath -Raw
$issue = Get-Content $issuePath -Raw
$launch = Get-Content $launchPath -Raw

$mainContracts = @(
    'private readonly IForgeEvidenceService _forgeEvidenceService;',
    'private readonly IForgeEvidenceBus _forgeEvidenceBus;',
    '_forgeEvidenceService = services.ForgeEvidenceService;',
    '_forgeEvidenceBus.Published += ForgeEvidenceBus_Published;',
    '_forgeEvidenceService.Invalidated += ForgeEvidenceService_Invalidated;',
    '_forgeEvidenceService.StartWatching(mods);',
    'await _forgeEvidenceService.RefreshAsync(',
    'ApplyForgeEvidenceSnapshot(snapshot);',
    'SharedEvidenceGeneration = snapshot.Generation'
)
foreach ($contract in $mainContracts) {
    if (-not $main.Contains($contract)) { throw "Missing consumer migration contract: $contract" }
}

$consumerContracts = @(
    'ForgeEvidenceSnapshot _forgeEvidenceSnapshot',
    'ForgeEvidenceBus_Published',
    'ForgeEvidenceService_Invalidated',
    'mod.Evidence = entry.Evidence',
    'The next refresh will rescan only this mod',
    'InvalidateSharedEvidence'
)
foreach ($contract in $consumerContracts) {
    if (-not (($consumer + $main).Contains($contract))) { throw "Missing shared snapshot consumer contract: $contract" }
}

if ($main.Contains('_modLibraryService.EnrichEvidenceAsync(')) {
    throw 'Legacy independent background evidence enrichment is still active.'
}
if (-not $issue.Contains('_analysisSnapshot')) {
    throw 'Issue Viewer no longer consumes the shared post-publication analysis snapshot.'
}
if (-not $launch.Contains('this performs no scan, file read, or analysis refresh')) {
    throw 'Launch Readiness cache-only contract was lost.'
}

Write-Host 'Pass46A2SharedEvidenceConsumerMigration-Test: PASSED'
