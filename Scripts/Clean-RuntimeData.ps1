param([switch]$IncludeGeneratedDatabase)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$folders = @('Output','Logs','Cache')
if ($IncludeGeneratedDatabase) { $folders += 'Database.Generated' }
foreach ($name in $folders) {
    $path = Join-Path $root $name
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}
Write-Host 'Runtime data cleaned.'
