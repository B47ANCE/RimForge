$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$model = Get-Content (Join-Path $root 'src\RimForge.Core\Models\ExternalProfileChange.cs') -Raw
$contract = Get-Content (Join-Path $root 'src\RimForge.Core\Services\NativeEngineInterfaces.cs') -Raw
$service = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ModsConfigChangeMonitor.cs') -Raw
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw

$checks = @(
    @{ Name = 'External change model exists'; Pass = $model.Contains('ExternalProfileChange') -and $model.Contains('PreviousSha256') -and $model.Contains('CurrentSha256') },
    @{ Name = 'Monitor contract is asynchronous and disposable'; Pass = $contract.Contains('interface IModsConfigChangeMonitor : IAsyncDisposable') -and $contract.Contains('AcknowledgeCurrentAsync') },
    @{ Name = 'Watcher is scoped to ModsConfig file'; Pass = $service.Contains('new FileSystemWatcher(directory, fileName)') -and $service.Contains('IncludeSubdirectories = false') },
    @{ Name = 'Events are debounced'; Pass = $service.Contains('Task.Delay(_debounce') -and $service.Contains('Interlocked.Exchange(ref _debounceCancellation') },
    @{ Name = 'Changes are content aware'; Pass = $service.Contains('SHA256.HashDataAsync') -and $service.Contains('string.Equals(previous, current') },
    @{ Name = 'RimForge writes can be acknowledged'; Pass = $service.Contains('AcknowledgeCurrentAsync') -and $service.Contains('_lastSha256 = await ComputeHashAsync') },
    @{ Name = 'Transient replacement access is tolerated'; Pass = $service.Contains('FileShare.ReadWrite | FileShare.Delete') -and $service.Contains('catch (IOException)') },
    @{ Name = 'Application composition owns monitor lifetime'; Pass = $composition.Contains('new ModsConfigChangeMonitor()') -and $composition.Contains('await ModsConfigChangeMonitor.DisposeAsync()') }
)

$failed = @($checks | Where-Object { -not $_.Pass })
$checks | ForEach-Object { Write-Host "[$(if ($_.Pass) {'PASS'} else {'FAIL'})] $($_.Name)" }
if ($failed.Count -gt 0) { throw "External ModsConfig change monitor contract failed: $($failed.Name -join ', ')" }
Write-Host 'External ModsConfig change monitor contract passed.' -ForegroundColor Green
