Set-StrictMode -Version Latest

function ConvertTo-VersionPackageId {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [string]$PackageId
    )

    if ([string]::IsNullOrWhiteSpace($PackageId)) {
        return $null
    }

    return $PackageId.Trim().ToLowerInvariant()
}

function Get-SafePropertyValue {
    [CmdletBinding()]
    param(
        [AllowNull()]
        $InputObject,

        [Parameter(Mandatory)]
        [string]$PropertyName,

        $DefaultValue = $null
    )

    if ($null -eq $InputObject) {
        return $DefaultValue
    }

    if ($InputObject.PSObject.Properties.Name -contains $PropertyName) {
        return $InputObject.$PropertyName
    }

    return $DefaultValue
}

function Test-ExternalRequestTimeout {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $ErrorRecord
    )

    $exception = $ErrorRecord.Exception

    while ($null -ne $exception) {
        if ($exception -is [System.TimeoutException]) {
            return $true
        }

        if ($exception -is [System.Net.WebException]) {
            if (
                $exception.Status -eq [System.Net.WebExceptionStatus]::Timeout -or
                $exception.Status -eq [System.Net.WebExceptionStatus]::NameResolutionFailure -or
                $exception.Status -eq [System.Net.WebExceptionStatus]::ConnectFailure
            ) {
                return $true
            }
        }

        if (
            ([string]$exception.Message) -match
            '(?i)timed? out|timeout|name resolution|could not resolve|unable to connect|connection.*failed'
        ) {
            return $true
        }

        $exception = $exception.InnerException
    }

    return $false
}

function Test-CacheFresh {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [int]$MaxAgeHours
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $age = (Get-Date).ToUniversalTime() -
        (Get-Item -LiteralPath $Path).LastWriteTimeUtc

    return ($age.TotalHours -lt $MaxAgeHours)
}

function Get-NoVersionWarningPackageIds {
    <#
    .SYNOPSIS
        Downloads and caches No Version Warning's version-specific database.
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$TargetVersion,

        [Parameter(Mandatory)]
        [string]$CacheFolder,

        [int]$CacheHours = 24,

        [ValidateRange(1, 300)]
        [int]$ExternalTimeoutSeconds = 10,

        [int]$ProgressId = 30,

        [switch]$DisableProgress
    )

    if (-not (Test-Path -LiteralPath $CacheFolder)) {
        New-Item `
            -ItemType Directory `
            -Path $CacheFolder `
            -Force |
            Out-Null
    }

    $safeVersion = $TargetVersion -replace '[^0-9.]', ""
    $cachePath = Join-Path `
        $CacheFolder `
        ("NoVersionWarning-{0}.xml" -f $safeVersion)

    $sourceUrl = (
        "https://raw.githubusercontent.com/" +
        "emipa606/NoVersionWarning/main/{0}/ModIdsToFix.xml"
    ) -f $safeVersion

    $usedCache = $false
    $downloadError = $null
    $timedOut = $false

    if (-not $DisableProgress) {
        Write-Progress `
            -Id $ProgressId `
            -Activity "Checking No Version Warning database" `
            -Status "Checking local cache..." `
            -PercentComplete 10
    }

    if (-not (Test-CacheFresh `
        -Path $cachePath `
        -MaxAgeHours $CacheHours)) {

        try {
            if (-not $DisableProgress) {
                Write-Progress `
                    -Id $ProgressId `
                    -Activity "Checking No Version Warning database" `
                    -Status ("Downloading RimWorld {0} compatibility data..." -f $safeVersion) `
                    -PercentComplete 45
            }

            Invoke-WebRequest `
                -Uri $sourceUrl `
                -OutFile $cachePath `
                -UseBasicParsing `
                -TimeoutSec $ExternalTimeoutSeconds `
                -ErrorAction Stop |
                Out-Null
        }
        catch {
            $downloadError = $_.Exception.Message
            $timedOut = Test-ExternalRequestTimeout -ErrorRecord $_

            if (Test-Path -LiteralPath $cachePath -PathType Leaf) {
                $usedCache = $true
            }
        }
    }
    else {
        $usedCache = $true
    }

    if (-not $DisableProgress) {
        $statusText = if ($usedCache) {
            "Using cached database."
        }
        elseif ($timedOut) {
            "Timed out; skipping online database."
        }
        elseif ($null -ne $downloadError) {
            "Online check failed; continuing without it."
        }
        else {
            "Database downloaded."
        }

        Write-Progress `
            -Id $ProgressId `
            -Activity "Checking No Version Warning database" `
            -Status $statusText `
            -PercentComplete 100 `
            -Completed
    }

    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
        return [PSCustomObject]@{
            Available      = $false
            SourceUrl      = $sourceUrl
            CachePath      = $cachePath
            UsedCache      = $false
            DownloadError  = $downloadError
            TimedOut       = $timedOut
            PackageIds     = @()
            PackageIdCount = 0
        }
    }

    try {
        $xml = New-Object System.Xml.XmlDocument
        $xml.PreserveWhitespace = $false
        $xml.Load($cachePath)

        $values = @()

        # Current database shape uses <li>package.id</li>. The fallback
        # supports future simple leaf-node layouts without hardcoding root.
        $nodes = @($xml.SelectNodes("//li"))

        if (@($nodes).Count -eq 0) {
            $nodes = @($xml.SelectNodes("//*[not(*)]"))
        }

        foreach ($node in @($nodes)) {
            $value = ([string]$node.InnerText).Trim()
            $normalized = ConvertTo-VersionPackageId -PackageId $value

            if (
                $null -ne $normalized -and
                $normalized -match '^[a-z0-9_.-]+$' -and
                $values -notcontains $normalized
            ) {
                $values += $normalized
            }
        }

        return [PSCustomObject]@{
            Available      = $true
            SourceUrl      = $sourceUrl
            CachePath      = $cachePath
            UsedCache      = $usedCache
            DownloadError  = $downloadError
            TimedOut       = $timedOut
            PackageIds     = @($values)
            PackageIdCount = @($values).Count
        }
    }
    catch {
        return [PSCustomObject]@{
            Available      = $false
            SourceUrl      = $sourceUrl
            CachePath      = $cachePath
            UsedCache      = $usedCache
            DownloadError  = $_.Exception.Message
            TimedOut       = $timedOut
            PackageIds     = @()
            PackageIdCount = 0
        }
    }
}

function Get-SteamWorkshopMetadata {
    <#
    .SYNOPSIS
        Retrieves public Workshop metadata in batches and caches the result.

    .DESCRIPTION
        Uses Valve's public GetPublishedFileDetails endpoint. No Steam API key
        is required for this public method.
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$WorkshopIds,

        [Parameter(Mandatory)]
        [string]$CacheFolder,

        [int]$CacheHours = 24,

        [int]$BatchSize = 100,

        [ValidateRange(1, 300)]
        [int]$ExternalTimeoutSeconds = 10,

        [int]$ProgressId = 31,

        [switch]$DisableProgress
    )

    if (-not (Test-Path -LiteralPath $CacheFolder)) {
        New-Item `
            -ItemType Directory `
            -Path $CacheFolder `
            -Force |
            Out-Null
    }

    $cachePath = Join-Path $CacheFolder "WorkshopMetadata.json"
    $cache = @{}

    if (Test-Path -LiteralPath $cachePath -PathType Leaf) {
        try {
            $cachedData = Get-Content `
                -LiteralPath $cachePath `
                -Raw |
                ConvertFrom-Json

            foreach ($item in @($cachedData.Items)) {
                $cache[[string]$item.WorkshopId] = $item
            }
        }
        catch {
            $cache = @{}
        }
    }

    $nowUtc = (Get-Date).ToUniversalTime()
    $requestedIds = @(
        $WorkshopIds |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace([string]$_)
        } |
        ForEach-Object {
            ([string]$_).Trim()
        } |
        Sort-Object -Unique
    )

    $idsToRefresh = @()

    foreach ($id in @($requestedIds)) {
        if (-not $cache.ContainsKey($id)) {
            $idsToRefresh += $id
            continue
        }

        try {
            $checkedUtc = [datetime]::Parse(
                [string]$cache[$id].CheckedUtc
            ).ToUniversalTime()

            if (($nowUtc - $checkedUtc).TotalHours -ge $CacheHours) {
                $idsToRefresh += $id
            }
        }
        catch {
            $idsToRefresh += $id
        }
    }

    $endpoint = (
        "https://api.steampowered.com/" +
        "ISteamRemoteStorage/GetPublishedFileDetails/v1/"
    )

    $errors = @()
    $timedOut = $false
    $externalChecksSkipped = $false
    $batchCount = if (@($idsToRefresh).Count -gt 0) {
        [Math]::Ceiling(@($idsToRefresh).Count / [double]$BatchSize)
    }
    else {
        0
    }
    $batchNumber = 0

    if (-not $DisableProgress) {
        $initialSteamStatus = if ($batchCount -gt 0) {
            "Preparing $batchCount request batch(es)..."
        }
        else {
            "All requested Workshop metadata is cached."
        }

        $initialSteamPercent = if ($batchCount -gt 0) {
            0
        }
        else {
            100
        }

        Write-Progress `
            -Id $ProgressId `
            -Activity "Checking Steam Workshop metadata" `
            -Status $initialSteamStatus `
            -PercentComplete $initialSteamPercent
    }

    for (
        $offset = 0;
        $offset -lt @($idsToRefresh).Count;
        $offset += $BatchSize
    ) {
        $lastIndex = [Math]::Min(
            $offset + $BatchSize - 1,
            @($idsToRefresh).Count - 1
        )

        $batch = @($idsToRefresh[$offset..$lastIndex])
        $batchNumber++

        if (-not $DisableProgress) {
            $batchPercent = if ($batchCount -gt 0) {
                [Math]::Floor((($batchNumber - 1) / $batchCount) * 100)
            }
            else {
                100
            }

            Write-Progress `
                -Id $ProgressId `
                -Activity "Checking Steam Workshop metadata" `
                -Status ("Batch {0}/{1} ({2} item(s))" -f $batchNumber, $batchCount, @($batch).Count) `
                -CurrentOperation "Contacting Steam public Workshop endpoint" `
                -PercentComplete $batchPercent
        }

        $body = @{
            itemcount = @($batch).Count
        }

        for ($index = 0; $index -lt @($batch).Count; $index++) {
            $body["publishedfileids[$index]"] = $batch[$index]
        }

        try {
            $response = Invoke-RestMethod `
                -Method Post `
                -Uri $endpoint `
                -Body $body `
                -ContentType "application/x-www-form-urlencoded" `
                -TimeoutSec $ExternalTimeoutSeconds `
                -ErrorAction Stop

            foreach ($detail in @(
                $response.response.publishedfiledetails
            )) {
                $id = [string]$detail.publishedfileid
                $resultCode = [int]$detail.result

                $cache[$id] = [PSCustomObject]@{
                    WorkshopId       = $id
                    CheckedUtc       = $nowUtc.ToString("o")
                    ResultCode       = $resultCode
                    Available        = ($resultCode -eq 1)
                    Title            = [string]$detail.title
                    TimeCreatedUtc   = if ($detail.time_created) {
                        [DateTimeOffset]::FromUnixTimeSeconds(
                            [long]$detail.time_created
                        ).UtcDateTime.ToString("o")
                    }
                    else {
                        $null
                    }
                    TimeUpdatedUtc   = if ($detail.time_updated) {
                        [DateTimeOffset]::FromUnixTimeSeconds(
                            [long]$detail.time_updated
                        ).UtcDateTime.ToString("o")
                    }
                    else {
                        $null
                    }
                    Visibility       = $detail.visibility
                    Banned           = [bool]$detail.banned
                    FileSize         = $detail.file_size
                    ConsumerAppId    = $detail.consumer_app_id
                    CreatorSteamId   = [string]$detail.creator
                    PreviewUrl       = [string]$detail.preview_url
                    Error            = $null
                }
            }
        }
        catch {
            $errors += $_.Exception.Message

            if (Test-ExternalRequestTimeout -ErrorRecord $_) {
                $timedOut = $true
                $externalChecksSkipped = $true
                break
            }
        }
    }

    if (-not $DisableProgress) {
        $steamStatus = if ($externalChecksSkipped) {
            "Timed out or offline; skipped remaining Steam checks."
        }
        elseif ($batchCount -eq 0) {
            "Using cached Workshop metadata."
        }
        else {
            "Workshop metadata check complete."
        }

        Write-Progress `
            -Id $ProgressId `
            -Activity "Checking Steam Workshop metadata" `
            -Status $steamStatus `
            -PercentComplete 100 `
            -Completed
    }

    $cacheItems = @(
        foreach ($id in @($cache.Keys | Sort-Object)) {
            $cache[$id]
        }
    )

    [PSCustomObject]@{
        GeneratedUtc = $nowUtc.ToString("o")
        Items        = @($cacheItems)
    } |
        ConvertTo-Json -Depth 8 |
        Set-Content `
            -LiteralPath $cachePath `
            -Encoding UTF8

    return [PSCustomObject]@{
        CachePath = $cachePath
        Items                 = $cache
        Errors                = @($errors)
        TimedOut              = $timedOut
        ExternalChecksSkipped = $externalChecksSkipped
    }
}

function Get-LocalModTimestampUtc {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Mod
    )

    $timestamps = @()

    foreach ($path in @(
        [string]$Mod.RootPath,
        [string]$Mod.AboutPath
    )) {
        if (
            -not [string]::IsNullOrWhiteSpace($path) -and
            (Test-Path -LiteralPath $path)
        ) {
            $timestamps += (Get-Item -LiteralPath $path).LastWriteTimeUtc
        }
    }

    if (@($timestamps).Count -eq 0) {
        return $null
    }

    return @($timestamps | Sort-Object -Descending)[0]
}

function Test-ModVersionStatus {
    <#
    .SYNOPSIS
        Combines local metadata, No Version Warning, and Workshop metadata.
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Mods,

        [Parameter(Mandatory)]
        [string]$TargetVersion,

        [Parameter(Mandatory)]
        [string]$CacheFolder,

        [int]$CacheHours = 24,

        [ValidateRange(1, 300)]
        [int]$ExternalTimeoutSeconds = 10,

        [switch]$DisableProgress
    )

    $nvw = Get-NoVersionWarningPackageIds `
        -TargetVersion $TargetVersion `
        -CacheFolder $CacheFolder `
        -CacheHours $CacheHours `
        -ExternalTimeoutSeconds $ExternalTimeoutSeconds `
        -ProgressId 30 `
        -DisableProgress:$DisableProgress

    $nvwLookup = @{}

    foreach ($id in @($nvw.PackageIds)) {
        $nvwLookup[$id] = $true
    }

    $workshopIds = @(
        foreach ($mod in @($Mods)) {
            $isWorkshop = [bool](Get-SafePropertyValue `
                -InputObject $mod `
                -PropertyName "IsWorkshop" `
                -DefaultValue $false)

            $workshopId = [string](Get-SafePropertyValue `
                -InputObject $mod `
                -PropertyName "WorkshopID" `
                -DefaultValue $null)

            if (
                $isWorkshop -and
                -not [string]::IsNullOrWhiteSpace($workshopId)
            ) {
                $workshopId
            }
        }
    )

    $workshop = Get-SteamWorkshopMetadata `
        -WorkshopIds @($workshopIds) `
        -CacheFolder $CacheFolder `
        -CacheHours $CacheHours `
        -ExternalTimeoutSeconds $ExternalTimeoutSeconds `
        -ProgressId 31 `
        -DisableProgress:$DisableProgress

    $statuses = @()
    $statusIndex = 0
    $statusTotal = @($Mods).Count

    foreach ($mod in @($Mods)) {
        $statusIndex++

        if (-not $DisableProgress) {
            $statusPercent = if ($statusTotal -gt 0) {
                [Math]::Floor((($statusIndex - 1) / $statusTotal) * 100)
            }
            else {
                0
            }

            $statusName = [string](Get-SafePropertyValue `
                -InputObject $mod `
                -PropertyName "Name" `
                -DefaultValue ("Mod {0}" -f $statusIndex))

            Write-Progress `
                -Id 32 `
                -Activity "Evaluating version and Workshop status" `
                -Status ("[{0}/{1}] {2}" -f $statusIndex, $statusTotal, $statusName) `
                -PercentComplete $statusPercent
        }
        $normalizedPackageId = ConvertTo-VersionPackageId `
            -PackageId ([string]$mod.PackageId)

        $supportedVersionsValue = Get-SafePropertyValue `
            -InputObject $mod `
            -PropertyName "SupportedVersions" `
            -DefaultValue @()

        $declaredVersions = @(
            foreach ($version in @($supportedVersionsValue)) {
                $trimmedVersion = ([string]$version).Trim()

                if (-not [string]::IsNullOrWhiteSpace($trimmedVersion)) {
                    $trimmedVersion
                }
            }
        )

        $nativeSupport = $declaredVersions -contains $TargetVersion
        $nvwVerified = (
            -not $nativeSupport -and
            $null -ne $normalizedPackageId -and
            $nvwLookup.ContainsKey($normalizedPackageId)
        )

        $compatibilityStatus = if ($nativeSupport) {
            "NativeSupport"
        }
        elseif ($nvwVerified) {
            "NoVersionWarningVerified"
        }
        else {
            "UnsupportedOrUnknown"
        }

        $workshopStatus = "NotWorkshop"
        $workshopTitle = $null
        $workshopUpdatedUtc = $null
        $localTimestampUtc = Get-LocalModTimestampUtc -Mod $mod
        $freshnessStatus = "NotApplicable"
        $workshopResultCode = $null

        $isWorkshop = [bool](Get-SafePropertyValue `
            -InputObject $mod `
            -PropertyName "IsWorkshop" `
            -DefaultValue $false)

        $modWorkshopId = [string](Get-SafePropertyValue `
            -InputObject $mod `
            -PropertyName "WorkshopID" `
            -DefaultValue $null)

        if (
            $isWorkshop -and
            -not [string]::IsNullOrWhiteSpace($modWorkshopId)
        ) {
            $id = $modWorkshopId

            if ($workshop.Items.ContainsKey($id)) {
                $metadata = $workshop.Items[$id]
                $workshopResultCode = $metadata.ResultCode
                $workshopTitle = $metadata.Title
                $workshopUpdatedUtc = $metadata.TimeUpdatedUtc

                if ($metadata.Available) {
                    $workshopStatus = "Available"

                    if (
                        $null -ne $localTimestampUtc -and
                        -not [string]::IsNullOrWhiteSpace(
                            [string]$workshopUpdatedUtc
                        )
                    ) {
                        $remoteUtc = [datetime]::Parse(
                            [string]$workshopUpdatedUtc
                        ).ToUniversalTime()

                        # Allow a small clock/filesystem tolerance.
                        if ($localTimestampUtc -lt $remoteUtc.AddMinutes(-5)) {
                            $freshnessStatus = "PossiblyStaleLocalCopy"
                        }
                        else {
                            $freshnessStatus = "CurrentOrNewer"
                        }
                    }
                    else {
                        $freshnessStatus = "Unknown"
                    }
                }
                else {
                    $workshopStatus = "UnavailableOrRemoved"
                    $freshnessStatus = "Unknown"
                }
            }
            else {
                $workshopStatus = "LookupFailed"
                $freshnessStatus = "Unknown"
            }
        }

        $statuses += [PSCustomObject]@{
            Name                  = Get-SafePropertyValue -InputObject $mod -PropertyName "Name"
            PackageId             = Get-SafePropertyValue -InputObject $mod -PropertyName "PackageId"
            WorkshopId            = $modWorkshopId
            RootPath              = Get-SafePropertyValue -InputObject $mod -PropertyName "RootPath"
            TargetVersion         = $TargetVersion
            DeclaredVersions      = @($declaredVersions)
            CompatibilityStatus   = $compatibilityStatus
            NativeSupport         = $nativeSupport
            NoVersionWarningMatch = $nvwVerified
            WorkshopStatus        = $workshopStatus
            WorkshopResultCode    = $workshopResultCode
            WorkshopTitle         = $workshopTitle
            WorkshopUpdatedUtc    = $workshopUpdatedUtc
            LocalTimestampUtc     = if ($null -ne $localTimestampUtc) {
                $localTimestampUtc.ToString("o")
            }
            else {
                $null
            }
            FreshnessStatus       = $freshnessStatus
        }
    }

    if (-not $DisableProgress) {
        Write-Progress `
            -Id 32 `
            -Activity "Evaluating version and Workshop status" `
            -Status "Version evaluation complete." `
            -PercentComplete 100 `
            -Completed
    }

    return [PSCustomObject]@{
        TargetVersion            = $TargetVersion
        GeneratedUtc             = (Get-Date).ToUniversalTime().ToString("o")
        NoVersionWarning         = $nvw
        WorkshopLookupErrors     = @($workshop.Errors)
        ExternalTimeoutSeconds   = $ExternalTimeoutSeconds
        NoVersionWarningTimedOut = [bool]$nvw.TimedOut
        SteamTimedOut            = [bool]$workshop.TimedOut
        ExternalChecksSkipped    = (
            [bool]$nvw.TimedOut -or
            [bool]$workshop.ExternalChecksSkipped
        )
        TotalMods                = @($statuses).Count
        NativeSupportCount       = @(
            $statuses |
            Where-Object {
                $_.CompatibilityStatus -eq "NativeSupport"
            }
        ).Count
        NoVersionWarningCount    = @(
            $statuses |
            Where-Object {
                $_.CompatibilityStatus -eq "NoVersionWarningVerified"
            }
        ).Count
        UnsupportedUnknownCount  = @(
            $statuses |
            Where-Object {
                $_.CompatibilityStatus -eq "UnsupportedOrUnknown"
            }
        ).Count
        WorkshopUnavailableCount = @(
            $statuses |
            Where-Object {
                $_.WorkshopStatus -eq "UnavailableOrRemoved"
            }
        ).Count
        PossiblyStaleCount       = @(
            $statuses |
            Where-Object {
                $_.FreshnessStatus -eq "PossiblyStaleLocalCopy"
            }
        ).Count
        Mods                     = @($statuses)
    }
}

function Get-ProfileVersionSummary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Profile,

        [Parameter(Mandatory)]
        $VersionStatus
    )

    $statusLookup = @{}

    foreach ($item in @($VersionStatus.Mods)) {
        $id = ConvertTo-VersionPackageId `
            -PackageId ([string]$item.PackageId)

        if ($null -ne $id) {
            $statusLookup[$id] = $item
        }
    }

    $items = @()

    foreach ($entry in @($Profile.ActiveMods)) {
        if ($statusLookup.ContainsKey($entry.NormalizedPackageId)) {
            $items += $statusLookup[$entry.NormalizedPackageId]
        }
    }

    return [PSCustomObject]@{
        ProfileName              = $Profile.Name
        ModCount                 = $Profile.Count
        NativeSupportCount       = @(
            $items |
            Where-Object {
                $_.CompatibilityStatus -eq "NativeSupport"
            }
        ).Count
        NoVersionWarningCount    = @(
            $items |
            Where-Object {
                $_.CompatibilityStatus -eq "NoVersionWarningVerified"
            }
        ).Count
        UnsupportedUnknownCount  = @(
            $items |
            Where-Object {
                $_.CompatibilityStatus -eq "UnsupportedOrUnknown"
            }
        ).Count
        WorkshopUnavailableCount = @(
            $items |
            Where-Object {
                $_.WorkshopStatus -eq "UnavailableOrRemoved"
            }
        ).Count
        PossiblyStaleCount       = @(
            $items |
            Where-Object {
                $_.FreshnessStatus -eq "PossiblyStaleLocalCopy"
            }
        ).Count
        Mods                     = @($items)
    }
}

function Write-VersionStatusReports {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $VersionStatus,

        [Parameter(Mandatory)]
        [array]$ProfileSummaries,

        [Parameter(Mandatory)]
        [string]$OutputFolder
    )

    if (-not (Test-Path -LiteralPath $OutputFolder)) {
        New-Item `
            -ItemType Directory `
            -Path $OutputFolder `
            -Force |
            Out-Null
    }

    $fullReport = [PSCustomObject]@{
        VersionStatus    = $VersionStatus
        ProfileSummaries = @($ProfileSummaries)
    }

    $jsonPath = Join-Path $OutputFolder "VersionStatus.json"

    $fullReport |
        ConvertTo-Json -Depth 12 |
        Set-Content `
            -LiteralPath $jsonPath `
            -Encoding UTF8

    @(
        $VersionStatus.Mods |
        Where-Object {
            $_.CompatibilityStatus -eq "UnsupportedOrUnknown"
        } |
        ForEach-Object {
            "{0}`t{1}" -f $_.PackageId, $_.Name
        }
    ) | Set-Content `
        -LiteralPath (Join-Path $OutputFolder "UnsupportedOrUnknown.txt") `
        -Encoding UTF8

    @(
        $VersionStatus.Mods |
        Where-Object {
            $_.CompatibilityStatus -eq "NoVersionWarningVerified"
        } |
        ForEach-Object {
            "{0}`t{1}" -f $_.PackageId, $_.Name
        }
    ) | Set-Content `
        -LiteralPath (Join-Path $OutputFolder "NoVersionWarningVerified.txt") `
        -Encoding UTF8

    @(
        $VersionStatus.Mods |
        Where-Object {
            $_.WorkshopStatus -eq "UnavailableOrRemoved"
        } |
        ForEach-Object {
            "{0}`t{1}`t{2}" -f
            $_.WorkshopId,
            $_.PackageId,
            $_.Name
        }
    ) | Set-Content `
        -LiteralPath (Join-Path $OutputFolder "WorkshopUnavailable.txt") `
        -Encoding UTF8

    @(
        $VersionStatus.Mods |
        Where-Object {
            $_.FreshnessStatus -eq "PossiblyStaleLocalCopy"
        } |
        ForEach-Object {
            "{0}`t{1}`tRemote={2}`tLocal={3}" -f
            $_.WorkshopId,
            $_.PackageId,
            $_.WorkshopUpdatedUtc,
            $_.LocalTimestampUtc
        }
    ) | Set-Content `
        -LiteralPath (Join-Path $OutputFolder "PossiblyStaleLocalMods.txt") `
        -Encoding UTF8

    Write-Log SUCCESS (
        "Version-status reports written to {0}" -f $OutputFolder
    )

    return $jsonPath
}

Export-ModuleMember -Function `
    Get-NoVersionWarningPackageIds,
    Get-SteamWorkshopMetadata,
    Test-ModVersionStatus,
    Get-ProfileVersionSummary,
    Write-VersionStatusReports
