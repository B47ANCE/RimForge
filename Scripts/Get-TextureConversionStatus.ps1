param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$processes = @(
    Get-Process texconv -ErrorAction SilentlyContinue
)

if (@($processes).Count -eq 0) {
    Write-Host "No texconv process is currently running."
    exit 0
}

$processes |
    Select-Object `
        Id,
        CPU,
        StartTime,
        WorkingSet64,
        Responding |
    Format-Table -AutoSize

Write-Host (
    "{0} texconv process(es) are active." -f
    @($processes).Count
)
