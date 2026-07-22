$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $PSScriptRoot 'RimForge.ExecutionTests\RimForge.ExecutionTests.csproj'

if (-not (Test-Path -LiteralPath $project)) {
    throw "Background task lifecycle harness is missing: $project"
}

& dotnet run --project $project --configuration Debug --no-build
if ($LASTEXITCODE -ne 0) {
    throw "RimForge.ExecutionTests failed with exit code $LASTEXITCODE."
}

Write-Host 'BackgroundTaskLifecycle-Test: PASSED' -ForegroundColor Green
