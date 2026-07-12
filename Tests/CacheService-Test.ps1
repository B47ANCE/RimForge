Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $root 'Modules\CacheService.psm1') -Force -DisableNameChecking -ErrorAction Stop

$temp = Join-Path ([IO.Path]::GetTempPath()) ('rimforge-cache-test-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    $source = Join-Path $temp 'source.txt'
    Set-Content -LiteralPath $source -Value 'one' -Encoding UTF8
    Write-RimForgeCacheEntry -CacheRoot $temp -Namespace 'Tests' -Key 'entry' -Value ([PSCustomObject]@{ Result = 42 }) -SourcePath $source | Out-Null
    $hit = Read-RimForgeCacheEntry -CacheRoot $temp -Namespace 'Tests' -Key 'entry' -SourcePath $source
    if ($null -eq $hit -or [int]$hit.Value.Result -ne 42) { throw 'Valid cache entry was not returned.' }
    Start-Sleep -Milliseconds 25
    Set-Content -LiteralPath $source -Value 'changed-value' -Encoding UTF8
    $miss = Read-RimForgeCacheEntry -CacheRoot $temp -Namespace 'Tests' -Key 'entry' -SourcePath $source
    if ($null -ne $miss) { throw 'Stale cache entry was not rejected.' }
    Write-Host 'RimForge cache service tests passed.' -ForegroundColor Green
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue }
