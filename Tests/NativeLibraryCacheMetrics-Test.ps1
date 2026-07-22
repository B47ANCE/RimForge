$fixture = Join-Path (Split-Path -Parent $PSScriptRoot) 'Database/TextureRules.json'
if (-not (Test-Path $fixture)) { Write-Host 'Startup metrics fixture is not present; run RimForge startup before this runtime test.'; exit 0 } # prerequisite skip
param(
    [string]$Path = (Join-Path $PSScriptRoot '..\Output\Reports\StartupMetrics.json'),
    [switch]$RequireHits
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
$report = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json

if ($null -eq $report.NativeLibraryCache) {
    throw "Startup metrics do not contain NativeLibraryCache diagnostics."
}

$cache = $report.NativeLibraryCache
foreach ($property in @(
    'CachePath', 'LoadStatus', 'DiscoveredFolders', 'CachedEntries',
    'CacheHits', 'CacheMisses', 'AddedEntries', 'RemovedEntries',
    'ReparsedEntries', 'LoadMilliseconds', 'SignatureMilliseconds',
    'ParseMilliseconds', 'SaveMilliseconds', 'MissReasons')) {
    if ($cache.PSObject.Properties.Name -notcontains $property) {
        throw "NativeLibraryCache diagnostics are missing '$property'."
    }
}

if ($RequireHits -and [int]$cache.CacheHits -le 0) {
    $reasons = @($cache.MissReasons | ForEach-Object { "$($_.Reason)=$($_.Count)" }) -join ', '
    throw "No native-library cache hits were recorded. LoadStatus=$($cache.LoadStatus); MissReasons=$reasons"
}

Write-Host "Native-library cache metrics test passed."
Write-Host "LoadStatus=$($cache.LoadStatus); Cached=$($cache.CachedEntries); Hits=$($cache.CacheHits); Misses=$($cache.CacheMisses); Reparsed=$($cache.ReparsedEntries)"
