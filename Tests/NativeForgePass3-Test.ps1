$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$required = @(
  'src\RimForge.Analysis\Services\ModNameResolver.cs',
  'src\RimForge.Analysis\Models\AnalysisModels.cs',
  'src\RimForge.Analysis\Services\ModAnalysisEngine.cs',
  'src\RimForge.App\Forge\NativeForgeRunner.cs'
)
foreach ($relative in $required) {
  if (-not (Test-Path (Join-Path $root $relative))) { throw "Missing required file: $relative" }
}
$engine = Get-Content (Join-Path $root 'src\RimForge.Analysis\Services\ModAnalysisEngine.cs') -Raw
$models = Get-Content (Join-Path $root 'src\RimForge.Analysis\Models\AnalysisModels.cs') -Raw
$runner = Get-Content (Join-Path $root 'src\RimForge.App\Forge\NativeForgeRunner.cs') -Raw
if ($engine -notmatch 'ModNameResolver\.Normalize') { throw 'Package-ID normalization is not wired into the graph engine.' }
if ($models -notmatch 'record LoadOrderPlan') { throw 'Native LoadOrderPlan model is missing.' }
if ($runner -notmatch 'snapshot\.LoadOrderPlan') { throw 'Native Forge report is not using LoadOrderPlan.' }
if ($runner -notmatch 'mod\.IsOfficialContent') { throw 'Official-content metadata handling is missing.' }
if ($runner -notmatch 'NativeEvidenceReport\.json') { throw 'Native Evidence reporting regressed.' }
if ($runner -notmatch 'NativeCompatibilityReport\.json') { throw 'Native compatibility reporting regressed.' }
Write-Host 'Native Forge Pass 3 test passed.'
