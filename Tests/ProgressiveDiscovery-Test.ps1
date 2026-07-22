$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$interface = Get-Content (Join-Path $root 'src/RimForge.Core/Services/NativeEngineInterfaces.cs') -Raw
$service = Get-Content (Join-Path $root 'src/RimForge.Infrastructure/Services/ModLibraryService.cs') -Raw
$window = Get-Content (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs') -Raw
if ($interface -notmatch 'includeEvidence = true') { throw 'ScanAsync does not expose evidence deferral.' }
if ($interface -notmatch 'EnrichEvidenceAsync') { throw 'Background intelligence contract is missing.' }
if ($window -notmatch 'includeEvidence: false') { throw 'Startup still requests blocking Evidence.' }
if ($window -notmatch 'IntelligenceMetrics.json') { throw 'Background intelligence metrics are missing.' }
if ($service -notmatch '!includeEvidence') { throw 'ModLibraryService does not skip Evidence during discovery.' }
Write-Host 'Progressive discovery architecture test passed.'
