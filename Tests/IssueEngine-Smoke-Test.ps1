$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$model = Join-Path $root 'src\RimForge.Analysis\Models\IssueModels.cs'
$engine = Join-Path $root 'src\RimForge.Analysis\Services\IssueEngine.cs'
if (-not (Test-Path $model)) { throw 'IssueModels.cs was not found.' }
if (-not (Test-Path $engine)) { throw 'IssueEngine.cs was not found.' }
$modelText = Get-Content $model -Raw
$engineText = Get-Content $engine -Raw
foreach ($token in @('IssueWorkItem','IssueScopeSummary','CanonicalStatus','IssueViewerSnapshot')) {
    if ($modelText -notmatch [regex]::Escape($token)) { throw "Missing issue model token: $token" }
}
foreach ($token in @('MissingRequiredDependency','LoadOrderViolation','DuplicatePackageId','DependencyCycle','CanAutoFix')) {
    if ($engineText -notmatch [regex]::Escape($token)) { throw "Missing issue engine token: $token" }
}
Write-Host 'Issue Engine smoke test passed.'
