$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$enginePath = Join-Path $root 'src\RimForge.Analysis\Services\ModAnalysisEngine.cs'
$modelsPath = Join-Path $root 'src\RimForge.Analysis\Models\AnalysisModels.cs'
$runnerPath = Join-Path $root 'src\RimForge.App\Forge\NativeForgeRunner.cs'
foreach ($path in @($enginePath, $modelsPath, $runnerPath)) {
  if (-not (Test-Path $path)) { throw "Missing required file: $path" }
}
$engine = Get-Content $enginePath -Raw
$models = Get-Content $modelsPath -Raw
$runner = Get-Content $runnerPath -Raw
if ($models -notmatch 'LoadOrderBlockedByCycle') { throw 'Blocked-by-cycle issue type is missing.' }
if ($engine -notmatch 'Load order blocked by dependency cycle') { throw 'Blocked load-order issue generation is missing.' }
if ($runner -notmatch 'ProfileIssues') { throw 'Native report does not separate library and profile issue scopes.' }
if ($runner -notmatch 'MetadataErrors = mod\.IsOfficialContent') { throw 'Official-content metadata filtering is missing.' }
if ($runner -notmatch 'NativeEvidenceReport\.json' -or $runner -notmatch 'NativeCompatibilityReport\.json') { throw 'Native report parity was not preserved.' }
Write-Host 'Native Forge Pass 3.1 test passed.'
