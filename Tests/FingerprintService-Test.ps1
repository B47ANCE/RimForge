Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $root 'Modules\FingerprintService.psm1') -Force -DisableNameChecking
$temp = Join-Path ([IO.Path]::GetTempPath()) ('RimForge-Fingerprint-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $temp 'About.xml') -Value '<ModMetaData />' -Encoding UTF8
    $first = Get-RimForgeDirectoryFingerprint -RootPath $temp
    $second = Get-RimForgeDirectoryFingerprint -RootPath $temp
    if ($first.Fingerprint -ne $second.Fingerprint) { throw 'Unchanged folder fingerprint was unstable.' }
    Start-Sleep -Milliseconds 30
    Add-Content -LiteralPath (Join-Path $temp 'About.xml') -Value '<!-- changed -->'
    $third = Get-RimForgeDirectoryFingerprint -RootPath $temp
    if ($first.Fingerprint -eq $third.Fingerprint) { throw 'Changed folder retained the same fingerprint.' }

    $mod = [PSCustomObject]@{
        RootPath = $temp
        WorkshopID = '123456'
        FolderName = 'fingerprint-test'
    }
    $full = Get-RimForgeModFingerprint -Mod $mod -ForceFull
    $reused = Get-RimForgeModFingerprint -Mod $mod -PreviousFingerprint $full -FullVerificationIntervalDays 7
    if (-not $reused.ReusedFullFingerprint) { throw 'Fast unchanged signature did not reuse the full fingerprint.' }
    if ($reused.Fingerprint -ne $full.Fingerprint) { throw 'Reused fingerprint did not match the previous full fingerprint.' }
    Write-Host 'RimForge fingerprint service tests passed.' -ForegroundColor Green
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue }
