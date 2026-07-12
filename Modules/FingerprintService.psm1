Set-StrictMode -Version Latest

function Get-RimForgeStableHash {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Text)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-', '').ToLowerInvariant()
    }
    finally { $sha.Dispose() }
}

function Get-RimForgeFastModSignature {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RootPath)

    if (-not (Test-Path -LiteralPath $RootPath -PathType Container)) { return $null }

    $root = Get-Item -LiteralPath $RootPath
    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine('rimforge-fast-mod-signature-v1')
    [void]$builder.AppendLine($root.FullName.ToLowerInvariant())
    [void]$builder.AppendLine([string]$root.LastWriteTimeUtc.Ticks)

    $aboutCandidates = @(
        Join-Path $RootPath 'About\About.xml'
        Join-Path $RootPath 'About.xml'
    )
    foreach ($path in $aboutCandidates) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $file = Get-Item -LiteralPath $path
            [void]$builder.AppendLine(('{0}|{1}|{2}' -f $file.FullName.ToLowerInvariant(),$file.Length,$file.LastWriteTimeUtc.Ticks))
        }
    }

    foreach ($item in @(Get-ChildItem -LiteralPath $RootPath -Force -ErrorAction SilentlyContinue | Sort-Object Name)) {
        [void]$builder.AppendLine(('{0}|{1}|{2}' -f $item.Name.ToLowerInvariant(),$item.PSIsContainer,$item.LastWriteTimeUtc.Ticks))
    }

    return Get-RimForgeStableHash -Text $builder.ToString()
}

function Get-RimForgeDirectoryFingerprint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RootPath,
        [string[]]$IncludeExtensions = @('.xml','.dll','.png','.dds','.json'),
        [string[]]$ExcludeDirectoryNames = @('.git','Cache','Logs','Output','TextureWork'),
        [switch]$IncludeAllFiles
    )

    if (-not (Test-Path -LiteralPath $RootPath -PathType Container)) { return $null }

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine('rimforge-directory-fingerprint-v2')
    $fileCount = 0
    $totalBytes = [long]0
    $latestTicks = [long]0

    $files = @(Get-ChildItem -LiteralPath $RootPath -File -Recurse -ErrorAction SilentlyContinue | Where-Object {
        $relative = $_.FullName.Substring($RootPath.Length).TrimStart([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
        $parts = @($relative -split '[\\/]')
        foreach ($excluded in $ExcludeDirectoryNames) {
            if ($parts -contains $excluded) { return $false }
        }
        return ($IncludeAllFiles -or $IncludeExtensions -contains $_.Extension.ToLowerInvariant())
    } | Sort-Object FullName)

    foreach ($file in $files) {
        $relative = $file.FullName.Substring($RootPath.Length).TrimStart([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar).ToLowerInvariant()
        [void]$builder.Append($relative)
        [void]$builder.Append('|')
        [void]$builder.Append([string]$file.Length)
        [void]$builder.Append('|')
        [void]$builder.AppendLine([string]$file.LastWriteTimeUtc.Ticks)
        $fileCount++
        $totalBytes += [long]$file.Length
        if ($file.LastWriteTimeUtc.Ticks -gt $latestTicks) { $latestTicks = $file.LastWriteTimeUtc.Ticks }
    }

    [PSCustomObject]@{
        SchemaVersion = 2
        RootPath = (Get-Item -LiteralPath $RootPath).FullName
        Fingerprint = Get-RimForgeStableHash -Text $builder.ToString()
        FileCount = $fileCount
        TotalBytes = $totalBytes
        LatestWriteTimeUtcTicks = $latestTicks
        CalculatedUtc = [DateTime]::UtcNow.ToString('o')
    }
}

function Get-RimForgeModFingerprint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Mod,
        [AllowNull()]$PreviousFingerprint,
        [int]$FullVerificationIntervalDays = 7,
        [switch]$ForceFull
    )

    $rootPath = [string]$Mod.RootPath
    $key = if (-not [string]::IsNullOrWhiteSpace([string]$Mod.WorkshopID)) { 'workshop-' + [string]$Mod.WorkshopID } else { 'local-' + [string]$Mod.FolderName }
    $fastSignature = Get-RimForgeFastModSignature -RootPath $rootPath
    $canReuse = $false

    if (-not $ForceFull -and $null -ne $PreviousFingerprint -and $null -ne $fastSignature) {
        $hasFast = $PreviousFingerprint.PSObject.Properties.Name -contains 'FastSignature'
        $hasDate = $PreviousFingerprint.PSObject.Properties.Name -contains 'CalculatedUtc'
        if ($hasFast -and [string]$PreviousFingerprint.FastSignature -eq [string]$fastSignature -and $hasDate) {
            $calculated = [DateTime]::MinValue
            if ([DateTime]::TryParse([string]$PreviousFingerprint.CalculatedUtc,[ref]$calculated)) {
                $canReuse = ([DateTime]::UtcNow - $calculated.ToUniversalTime()).TotalDays -lt $FullVerificationIntervalDays
            }
        }
    }

    if ($canReuse) {
        return [PSCustomObject]@{
            Key = $key
            FolderName = [string]$Mod.FolderName
            RootPath = $rootPath
            WorkshopId = [string]$Mod.WorkshopID
            FastSignature = $fastSignature
            Fingerprint = [string]$PreviousFingerprint.Fingerprint
            FileCount = [int]$PreviousFingerprint.FileCount
            TotalBytes = [long]$PreviousFingerprint.TotalBytes
            LatestWriteTimeUtcTicks = [long]$PreviousFingerprint.LatestWriteTimeUtcTicks
            CalculatedUtc = [string]$PreviousFingerprint.CalculatedUtc
            ReusedFullFingerprint = $true
        }
    }

    $directory = Get-RimForgeDirectoryFingerprint -RootPath $rootPath
    [PSCustomObject]@{
        Key = $key
        FolderName = [string]$Mod.FolderName
        RootPath = $rootPath
        WorkshopId = [string]$Mod.WorkshopID
        FastSignature = $fastSignature
        Fingerprint = if ($null -eq $directory) { $null } else { $directory.Fingerprint }
        FileCount = if ($null -eq $directory) { 0 } else { $directory.FileCount }
        TotalBytes = if ($null -eq $directory) { 0 } else { $directory.TotalBytes }
        LatestWriteTimeUtcTicks = if ($null -eq $directory) { 0 } else { $directory.LatestWriteTimeUtcTicks }
        CalculatedUtc = if ($null -eq $directory) { [DateTime]::UtcNow.ToString('o') } else { $directory.CalculatedUtc }
        ReusedFullFingerprint = $false
    }
}

function Get-RimForgeModFingerprintSet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][array]$Mods,
        [AllowNull()]$PreviousState,
        [int]$FullVerificationIntervalDays = 7,
        [switch]$DisableProgress,
        [switch]$ForceFull
    )

    $previousByKey = @{}
    if ($null -ne $PreviousState -and $PreviousState.PSObject.Properties.Name -contains 'Mods') {
        foreach ($item in @($PreviousState.Mods)) { $previousByKey[[string]$item.Key] = $item }
    }

    $results = @(); $index = 0; $total = @($Mods).Count
    foreach ($mod in @($Mods)) {
        $index++
        if (-not $DisableProgress -and $total -gt 0) {
            Write-Progress -Activity 'Fingerprinting mods' -Status "$index / $total" -PercentComplete (($index / $total) * 100)
        }
        $key = if (-not [string]::IsNullOrWhiteSpace([string]$mod.WorkshopID)) { 'workshop-' + [string]$mod.WorkshopID } else { 'local-' + [string]$mod.FolderName }
        $previous = if ($previousByKey.ContainsKey($key)) { $previousByKey[$key] } else { $null }
        $results += Get-RimForgeModFingerprint -Mod $mod -PreviousFingerprint $previous -FullVerificationIntervalDays $FullVerificationIntervalDays -ForceFull:$ForceFull
    }
    if (-not $DisableProgress) { Write-Progress -Activity 'Fingerprinting mods' -Completed }
    return @($results)
}

Export-ModuleMember -Function Get-RimForgeStableHash,Get-RimForgeFastModSignature,Get-RimForgeDirectoryFingerprint,Get-RimForgeModFingerprint,Get-RimForgeModFingerprintSet
