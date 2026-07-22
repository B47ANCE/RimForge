$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content (Join-Path $root 'src\RimForge.Core\Models\PlatformDiscoveryModels.cs') -Raw
$contracts = Get-Content (Join-Path $root 'src\RimForge.Core\Services\NativeEngineInterfaces.cs') -Raw
$service = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\PlatformDiscoveryService.cs') -Raw
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
$gameLaunch = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\GameLaunchService.cs') -Raw
$profiles = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ProfileWorkspaceService.cs') -Raw

$checks = @(
    @{ Name = 'Immutable discovery snapshot'; Pass = $models -match 'record PlatformDiscoverySnapshot' },
    @{ Name = 'RimWorld user paths'; Pass = $models -match 'record RimWorldUserPaths' },
    @{ Name = 'Platform discovery contract'; Pass = $contracts -match 'interface IPlatformDiscoveryService' },
    @{ Name = 'RimWorld installation contract'; Pass = $contracts -match 'interface IRimWorldInstallationService' },
    @{ Name = 'Steam library contract'; Pass = $contracts -match 'interface ISteamLibraryService' },
    @{ Name = 'Workspace contract'; Pass = $contracts -match 'interface IWorkspaceService' },
    @{ Name = 'Preferred installation policy'; Pass = $service -match 'OrderByDescending\(candidate => candidate\.CanLaunchDirectly\)' },
    @{ Name = 'Central Player.log path'; Pass = $gameLaunch -match 'platformDiscoveryService\.Discover\(\)\.UserPaths\.PlayerLogPath' },
    @{ Name = 'Central ModsConfig path'; Pass = $profiles -match '_platformDiscoveryService\.Discover\(\)\.UserPaths\.ModsConfigPath' },
    @{ Name = 'Single composed service graph'; Pass = $composition -match 'new PlatformDiscoveryService\(steamLibraryDiscoveryService, workspaceService\)' }
)

$failed = $false
foreach ($check in $checks) {
    if ($check.Pass) { Write-Host "[PASS] $($check.Name)" -ForegroundColor Green }
    else { Write-Host "[FAIL] $($check.Name)" -ForegroundColor Red; $failed = $true }
}
if ($failed) { exit 1 }
Write-Host 'Platform discovery foundation validation passed.' -ForegroundColor Green
