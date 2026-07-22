$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content (Join-Path $root 'src\RimForge.Core\Models\RuntimeModels.cs') -Raw
$contracts = Get-Content (Join-Path $root 'src\RimForge.Core\Services\RuntimeInterfaces.cs') -Raw
$manager = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ForgeSessionService.cs') -Raw
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
$forge = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw

$checks = @(
    @{ Name = 'Session identity'; Pass = $models -match 'record struct ForgeSessionId' },
    @{ Name = 'Explicit lifecycle'; Pass = $models -match 'enum ForgeSessionState' },
    @{ Name = 'Session metadata'; Pass = $models -match 'string Workspace' -and $models -match 'string GameVersion' -and $models -match 'int ModCount' },
    @{ Name = 'Manager contract'; Pass = $contracts -match 'interface IForgeSessionManager' },
    @{ Name = 'Owned cancellation'; Pass = $contracts -match 'CancellationToken CancellationToken' -and $manager -match '_cancellation\?\.Cancel\(\)' },
    @{ Name = 'Concurrency guard'; Pass = $manager -match 'is already active' },
    @{ Name = 'Atomic persistence'; Pass = $manager -match 'File\.Move\(temporary, path, true\)' },
    @{ Name = 'Interrupted recovery'; Pass = $manager -match 'ForgeSessionState\.Failed' -and $manager -match 'Stage = "Interrupted"' },
    @{ Name = 'Canonical sessions root'; Pass = $composition -match 'new ForgeSessionService\(eventBus, paths\.SessionsRoot, diagnosticService, sessionLog\)' },
    @{ Name = 'Forge integration'; Pass = $forge -match '_forgeSessionService\.Start\(new ForgeSessionRequest' }
)

$failed = $false
foreach ($check in $checks) {
    if ($check.Pass) { Write-Host "[PASS] $($check.Name)" -ForegroundColor Green }
    else { Write-Host "[FAIL] $($check.Name)" -ForegroundColor Red; $failed = $true }
}
if ($failed) { exit 1 }
Write-Host 'Forge Session foundation validation passed.' -ForegroundColor Green
