Set-StrictMode -Version Latest

function Resolve-RimForgeCachePath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$CacheRoot,
        [Parameter(Mandatory)][string]$Namespace,
        [Parameter(Mandatory)][string]$Key
    )

    $safeNamespace = ($Namespace -replace '[^A-Za-z0-9._-]', '_').Trim('_')
    $safeKey = ($Key -replace '[^A-Za-z0-9._-]', '_').Trim('_')
    if ([string]::IsNullOrWhiteSpace($safeNamespace)) { throw 'Cache namespace is empty.' }
    if ([string]::IsNullOrWhiteSpace($safeKey)) { throw 'Cache key is empty.' }

    return Join-Path (Join-Path $CacheRoot $safeNamespace) ($safeKey + '.json')
}

function Get-RimForgeFileFingerprint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [ValidateSet('Metadata','SHA256')][string]$Mode = 'Metadata'
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $file = Get-Item -LiteralPath $Path -Force

    $fingerprint = [ordered]@{
        Path = $file.FullName
        Length = [long]$file.Length
        LastWriteTimeUtcTicks = [long]$file.LastWriteTimeUtc.Ticks
        Mode = $Mode
        Hash = $null
    }

    if ($Mode -eq 'SHA256') {
        $fingerprint.Hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }

    return [PSCustomObject]$fingerprint
}

function Test-RimForgeFileFingerprint {
    [CmdletBinding()]
    param(
        [AllowNull()]$Fingerprint,
        [Parameter(Mandatory)][string]$Path
    )

    if ($null -eq $Fingerprint) { return $false }
    $mode = if ($Fingerprint.PSObject.Properties.Name -contains 'Mode') { [string]$Fingerprint.Mode } else { 'Metadata' }
    $current = Get-RimForgeFileFingerprint -Path $Path -Mode $mode
    if ($null -eq $current) { return $false }

    if ([long]$Fingerprint.Length -ne [long]$current.Length) { return $false }
    if ([long]$Fingerprint.LastWriteTimeUtcTicks -ne [long]$current.LastWriteTimeUtcTicks) { return $false }
    if ($mode -eq 'SHA256' -and [string]$Fingerprint.Hash -ne [string]$current.Hash) { return $false }
    return $true
}

function Read-RimForgeCacheEntry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$CacheRoot,
        [Parameter(Mandatory)][string]$Namespace,
        [Parameter(Mandatory)][string]$Key,
        [int]$SchemaVersion = 1,
        [AllowNull()][string]$SourcePath
    )

    $path = Resolve-RimForgeCachePath -CacheRoot $CacheRoot -Namespace $Namespace -Key $Key
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }

    try { $entry = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }
    catch { return $null }

    if (-not ($entry.PSObject.Properties.Name -contains 'SchemaVersion')) { return $null }
    if ([int]$entry.SchemaVersion -ne $SchemaVersion) { return $null }

    if (-not [string]::IsNullOrWhiteSpace($SourcePath)) {
        if (-not ($entry.PSObject.Properties.Name -contains 'SourceFingerprint')) { return $null }
        if (-not (Test-RimForgeFileFingerprint -Fingerprint $entry.SourceFingerprint -Path $SourcePath)) { return $null }
    }

    return $entry
}

function Write-RimForgeCacheEntry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$CacheRoot,
        [Parameter(Mandatory)][string]$Namespace,
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)]$Value,
        [int]$SchemaVersion = 1,
        [AllowNull()][string]$SourcePath,
        [ValidateSet('Metadata','SHA256')][string]$FingerprintMode = 'Metadata'
    )

    $path = Resolve-RimForgeCachePath -CacheRoot $CacheRoot -Namespace $Namespace -Key $Key
    $folder = Split-Path -Parent $path
    New-Item -ItemType Directory -Path $folder -Force | Out-Null

    $entry = [ordered]@{
        SchemaVersion = $SchemaVersion
        WrittenUtc = [DateTime]::UtcNow.ToString('o')
        SourceFingerprint = if ([string]::IsNullOrWhiteSpace($SourcePath)) { $null } else { Get-RimForgeFileFingerprint -Path $SourcePath -Mode $FingerprintMode }
        Value = $Value
    }

    $temporary = $path + '.tmp-' + [guid]::NewGuid().ToString('N')
    try {
        $entry | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $temporary -Encoding UTF8
        Move-Item -LiteralPath $temporary -Destination $path -Force
    }
    finally { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue }

    return $entry
}

function Remove-RimForgeCacheNamespace {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string]$CacheRoot,
        [Parameter(Mandatory)][string]$Namespace
    )
    $probe = Resolve-RimForgeCachePath -CacheRoot $CacheRoot -Namespace $Namespace -Key 'probe'
    $folder = Split-Path -Parent $probe
    if ((Test-Path -LiteralPath $folder -PathType Container) -and $PSCmdlet.ShouldProcess($folder, 'Remove cache namespace')) {
        Remove-Item -LiteralPath $folder -Recurse -Force
    }
}

Export-ModuleMember -Function Resolve-RimForgeCachePath,Get-RimForgeFileFingerprint,Test-RimForgeFileFingerprint,Read-RimForgeCacheEntry,Write-RimForgeCacheEntry,Remove-RimForgeCacheNamespace
