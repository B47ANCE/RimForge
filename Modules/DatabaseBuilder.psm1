Set-StrictMode -Version Latest

function Get-DatabaseSafeProperty {
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

function ConvertTo-DatabasePackageId {
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

function ConvertTo-DatabaseSafeFileName {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Value
    )

    $safe = $Value -replace '[^a-zA-Z0-9._-]', "_"

    if ([string]::IsNullOrWhiteSpace($safe)) {
        return "unknown"
    }

    return $safe
}

function Get-DatabaseSha256 {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Text
    )

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()

    try {
        return (
            [System.BitConverter]::ToString(
                $sha.ComputeHash($bytes)
            ) -replace "-", ""
        ).ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function ConvertTo-CanonicalJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $InputObject,

        [int]$Depth = 20
    )

    return (
        $InputObject |
        ConvertTo-Json -Depth $Depth -Compress
    )
}

function Set-DatabaseFileIfChanged {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Content
    )

    $parent = Split-Path -Parent $Path

    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force |
            Out-Null
    }

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $existing = Get-Content -LiteralPath $Path -Raw

        if ($existing -eq $Content) {
            return $false
        }
    }

    $temporaryPath = "{0}.{1}.tmp" -f
        $Path,
        [guid]::NewGuid().ToString("N")

    try {
        Set-Content `
            -LiteralPath $temporaryPath `
            -Value $Content `
            -Encoding UTF8

        Move-Item `
            -LiteralPath $temporaryPath `
            -Destination $Path `
            -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item `
                -LiteralPath $temporaryPath `
                -Force `
                -ErrorAction SilentlyContinue
        }
    }

    return $true
}

function Get-DatabaseWorkshopId {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Mod
    )

    foreach ($propertyName in @(
        "WorkshopId",
        "WorkshopID",
        "SteamWorkshopId",
        "PublishedFileId",
        "PublishedFileID"
    )) {
        $value = Get-DatabaseSafeProperty `
            -InputObject $Mod `
            -PropertyName $propertyName

        if ($null -eq $value) {
            continue
        }

        $text = [string]$value

        if ($text -match '^\d+$') {
            return $text
        }
    }

    $rootPath = [string](Get-DatabaseSafeProperty `
        -InputObject $Mod `
        -PropertyName "RootPath" `
        -DefaultValue "")

    if ($rootPath -match '[\\/]294100[\\/](\d+)([\\/]|$)') {
        return $Matches[1]
    }

    return $null
}

function Get-DatabaseModLookup {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Mods
    )

    $lookup = @{}

    foreach ($mod in @($Mods)) {
        $packageId = ConvertTo-DatabasePackageId `
            -PackageId ([string](Get-DatabaseSafeProperty `
                -InputObject $mod `
                -PropertyName "PackageId" `
                -DefaultValue ""))

        if ($null -ne $packageId -and -not $lookup.ContainsKey($packageId)) {
            $lookup[$packageId] = $mod
        }
    }

    return $lookup
}

function Get-DatabaseVersionLookup {
    [CmdletBinding()]
    param(
        [AllowNull()]
        $VersionStatus
    )

    $lookup = @{}

    if ($null -eq $VersionStatus) {
        return $lookup
    }

    foreach ($status in @(
        Get-DatabaseSafeProperty `
            -InputObject $VersionStatus `
            -PropertyName "Statuses" `
            -DefaultValue @()
    )) {
        $packageId = ConvertTo-DatabasePackageId `
            -PackageId ([string](Get-DatabaseSafeProperty `
                -InputObject $status `
                -PropertyName "PackageId" `
                -DefaultValue ""))

        if ($null -ne $packageId) {
            $lookup[$packageId] = $status
        }
    }

    return $lookup
}

function Resolve-DatabaseTaxonomy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$PackageId,

        [AllowNull()]
        $TaxonomyDatabase
    )

    if ($null -eq $TaxonomyDatabase) {
        return $null
    }

    foreach ($entry in @(
        Get-DatabaseSafeProperty `
            -InputObject $TaxonomyDatabase `
            -PropertyName "Entries" `
            -DefaultValue @()
    )) {
        $entryId = ConvertTo-DatabasePackageId `
            -PackageId ([string](Get-DatabaseSafeProperty `
                -InputObject $entry `
                -PropertyName "PackageId" `
                -DefaultValue ""))

        if ($entryId -eq $PackageId) {
            return [PSCustomObject][ordered]@{
                Source     = "Explicit"
                Family     = Get-DatabaseSafeProperty $entry "Family"
                Series     = Get-DatabaseSafeProperty $entry "Series"
                Module     = Get-DatabaseSafeProperty $entry "Module"
                Category   = Get-DatabaseSafeProperty $entry "Category"
                Roles      = @(
                    Get-DatabaseSafeProperty $entry "Roles" @()
                )
                Tags       = @(
                    Get-DatabaseSafeProperty $entry "Tags" @()
                )
                Confidence = Get-DatabaseSafeProperty $entry "Confidence"
                Reason     = Get-DatabaseSafeProperty $entry "Reason"
            }
        }
    }

    foreach ($rule in @(
        Get-DatabaseSafeProperty `
            -InputObject $TaxonomyDatabase `
            -PropertyName "FamilyRules" `
            -DefaultValue @()
    )) {
        $match = Get-DatabaseSafeProperty $rule "Match"
        $regex = [string](Get-DatabaseSafeProperty `
            -InputObject $match `
            -PropertyName "PackageIdRegex" `
            -DefaultValue "")

        if (
            -not [string]::IsNullOrWhiteSpace($regex) -and
            $PackageId -match $regex
        ) {
            return [PSCustomObject][ordered]@{
                Source     = "FamilyRule"
                RuleId     = Get-DatabaseSafeProperty $rule "Id"
                Family     = Get-DatabaseSafeProperty $rule "Family"
                Series     = Get-DatabaseSafeProperty $rule "Series"
                Module     = ($PackageId -split '\.')[-1]
                Category   = Get-DatabaseSafeProperty $rule "Category"
                Roles      = @(
                    Get-DatabaseSafeProperty $rule "Roles" @()
                )
                Tags       = @(
                    Get-DatabaseSafeProperty $rule "Tags" @()
                )
                Confidence = Get-DatabaseSafeProperty $rule "Confidence"
                Reason     = (
                    "Classified by family rule '{0}'." -f
                    (Get-DatabaseSafeProperty $rule "Id")
                )
            }
        }
    }

    return $null
}

function ConvertTo-PublishedEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $EvidenceResult
    )

    $raw = Get-DatabaseSafeProperty `
        -InputObject $EvidenceResult `
        -PropertyName "RawEvidence"

    $classification = Get-DatabaseSafeProperty `
        -InputObject $EvidenceResult `
        -PropertyName "Classification"

    if ($null -eq $raw) {
        return [PSCustomObject][ordered]@{
            Available = $false
            ScanError = Get-DatabaseSafeProperty $EvidenceResult "ScanError"
        }
    }

    return [PSCustomObject][ordered]@{
        Available            = $true
        XmlFileCount         = [int](Get-DatabaseSafeProperty $raw "XmlFileCount" 0)
        AssemblyFileCount    = [int](Get-DatabaseSafeProperty $raw "AssemblyFileCount" 0)
        XmlDefTypes          = Get-DatabaseSafeProperty $raw "XmlDefTypes"
        XmlElementNames      = Get-DatabaseSafeProperty $raw "XmlElementNames"
        XmlClassNames        = Get-DatabaseSafeProperty $raw "XmlClassNames"
        Dependencies         = @(
            Get-DatabaseSafeProperty $raw "Dependencies" @()
        )
        AssemblySignals      = @(
            Get-DatabaseSafeProperty $raw "AssemblyPatternHits" @()
        )
        XmlErrorCount        = @(
            Get-DatabaseSafeProperty $raw "XmlErrors" @()
        ).Count
        AssemblyErrorCount   = @(
            Get-DatabaseSafeProperty $raw "AssemblyErrors" @()
        ).Count
        SuggestedPrimary     = Get-DatabaseSafeProperty `
            -InputObject $classification `
            -PropertyName "PrimaryCategory"
        SuggestedPrimaryId   = Get-DatabaseSafeProperty `
            -InputObject $classification `
            -PropertyName "PrimaryCategoryId"
        Confidence           = [int](Get-DatabaseSafeProperty `
            -InputObject $classification `
            -PropertyName "Confidence" `
            -DefaultValue 0)
        NeedsReview          = [bool](Get-DatabaseSafeProperty `
            -InputObject $classification `
            -PropertyName "NeedsReview" `
            -DefaultValue $true)
        SecondarySuggestions = @(
            Get-DatabaseSafeProperty `
                -InputObject $classification `
                -PropertyName "Secondary" `
                -DefaultValue @()
        )
        RankedScores         = @(
            Get-DatabaseSafeProperty `
                -InputObject $classification `
                -PropertyName "RankedScores" `
                -DefaultValue @()
        )
    }
}

function Get-DatabaseRecordPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$DatabaseRoot,

        [AllowNull()]
        [string]$WorkshopId,

        [Parameter(Mandatory)]
        [string]$PackageId
    )

    if (-not [string]::IsNullOrWhiteSpace($WorkshopId)) {
        $shard = if ($WorkshopId.Length -ge 2) {
            $WorkshopId.Substring(0, 2)
        }
        else {
            $WorkshopId.PadLeft(2, "0")
        }

        return Join-Path `
            $DatabaseRoot `
            ("Mods\Workshop\{0}\{1}.json" -f $shard, $WorkshopId)
    }

    $hash = Get-DatabaseSha256 -Text $PackageId
    $safePackageId = ConvertTo-DatabaseSafeFileName -Value $PackageId

    return Join-Path `
        $DatabaseRoot `
        ("Mods\Package\{0}\{1}.json" -f $hash.Substring(0, 2), $safePackageId)
}

function New-PublishedModRecord {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $EvidenceResult,

        [AllowNull()]
        $Mod,

        [AllowNull()]
        $VersionStatus,

        [AllowNull()]
        $TaxonomyDatabase,

        [Parameter(Mandatory)]
        [string]$TargetVersion,

        [Parameter(Mandatory)]
        [string]$ScannerVersion,

        [AllowNull()]
        [string]$RulesFingerprint
    )

    $packageId = ConvertTo-DatabasePackageId `
        -PackageId ([string](Get-DatabaseSafeProperty `
            -InputObject $EvidenceResult `
            -PropertyName "PackageId" `
            -DefaultValue ""))

    if ($null -eq $packageId) {
        return $null
    }

    $workshopId = if ($null -ne $Mod) {
        Get-DatabaseWorkshopId -Mod $Mod
    }
    else {
        $null
    }

    $name = [string](Get-DatabaseSafeProperty `
        -InputObject $EvidenceResult `
        -PropertyName "Name" `
        -DefaultValue $packageId)

    $taxonomy = Resolve-DatabaseTaxonomy `
        -PackageId $packageId `
        -TaxonomyDatabase $TaxonomyDatabase

    $supportedVersions = @()

    if ($null -ne $Mod) {
        foreach ($propertyName in @(
            "SupportedVersions",
            "SupportedGameVersions",
            "Versions"
        )) {
            $value = Get-DatabaseSafeProperty `
                -InputObject $Mod `
                -PropertyName $propertyName

            if ($null -ne $value) {
                $supportedVersions = @($value)
                break
            }
        }
    }

    $publishedEvidence = ConvertTo-PublishedEvidence `
        -EvidenceResult $EvidenceResult

    $recordBody = [PSCustomObject][ordered]@{
        SchemaVersion = 1
        Identity      = [PSCustomObject][ordered]@{
            PackageId  = $packageId
            Name       = $name
            WorkshopId = $workshopId
        }
        Game          = [PSCustomObject][ordered]@{
            TargetVersion     = $TargetVersion
            SupportedVersions = @($supportedVersions)
        }
        Status        = if ($null -ne $VersionStatus) {
            [PSCustomObject][ordered]@{
                SupportStatus       = Get-DatabaseSafeProperty $VersionStatus "SupportStatus"
                NativeSupport       = Get-DatabaseSafeProperty $VersionStatus "NativeSupport"
                NoVersionWarning    = Get-DatabaseSafeProperty $VersionStatus "NoVersionWarning"
                WorkshopAvailable   = Get-DatabaseSafeProperty $VersionStatus "WorkshopAvailable"
                PossiblyStale       = Get-DatabaseSafeProperty $VersionStatus "PossiblyStale"
                WorkshopUpdatedUtc  = Get-DatabaseSafeProperty $VersionStatus "WorkshopUpdatedUtc"
                LocalUpdatedUtc     = Get-DatabaseSafeProperty $VersionStatus "LocalUpdatedUtc"
            }
        }
        else {
            $null
        }
        Taxonomy      = $taxonomy
        Evidence      = $publishedEvidence
        Provenance    = [PSCustomObject][ordered]@{
            Source                 = "AutomatedEvidence"
            ScannerVersion         = $ScannerVersion
            EvidenceRulesFingerprint = $RulesFingerprint
            ReviewRequired         = [bool](Get-DatabaseSafeProperty `
                -InputObject $publishedEvidence `
                -PropertyName "NeedsReview" `
                -DefaultValue $true)
        }
    }

    $canonicalBody = ConvertTo-CanonicalJson `
        -InputObject $recordBody `
        -Depth 24

    return [PSCustomObject][ordered]@{
        RecordHash = Get-DatabaseSha256 -Text $canonicalBody
        Record     = $recordBody
    }
}

function Write-DatabaseSupportFiles {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$DatabaseRoot
    )

    $curatedRoot = Join-Path `
        (Split-Path -Parent $DatabaseRoot) `
        "Database.Curated"

    if (-not (Test-Path -LiteralPath $curatedRoot -PathType Container)) {
        New-Item -ItemType Directory -Path $curatedRoot -Force |
            Out-Null
    }

    $templates = @{
        "ClassificationOverrides.json" = [PSCustomObject][ordered]@{
            SchemaVersion = 1
            Overrides     = @()
        }
        "CompatibilityRules.json" = [PSCustomObject][ordered]@{
            SchemaVersion = 1
            Rules         = @()
        }
        "LoadOrderRules.json" = [PSCustomObject][ordered]@{
            SchemaVersion = 1
            Rules         = @()
        }
        "PatchRules.json" = [PSCustomObject][ordered]@{
            SchemaVersion = 1
            Rules         = @()
        }
    }

    foreach ($name in @($templates.Keys)) {
        $path = Join-Path $curatedRoot $name

        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $json = $templates[$name] |
                ConvertTo-Json -Depth 10

            [void](Set-DatabaseFileIfChanged `
                -Path $path `
                -Content $json)
        }
    }

    $readmePath = Join-Path $DatabaseRoot "README.md"
    $readme = @"
# RimForge Generated Mod Database

This folder contains normalized, derived metadata produced by the RimWorld
Mod Auditor. It does not contain or redistribute Workshop mod payloads.

- `Mods/Workshop/` contains records keyed by Steam Workshop item ID.
- `Mods/Package/` contains records for mods without a Workshop ID.
- `Indexes/` contains package, Workshop, category, family, and review indexes.
- `Manifest.json` describes the latest database build.

Generated records are deterministic and only rewritten when their normalized
content changes.
"@

    [void](Set-DatabaseFileIfChanged `
        -Path $readmePath `
        -Content $readme)
}

function Export-RimForgeGeneratedDatabase {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Mods,

        [Parameter(Mandatory)]
        $EvidenceScan,

        [AllowNull()]
        $VersionStatus,

        [AllowNull()]
        $TaxonomyDatabase,

        [Parameter(Mandatory)]
        [string]$DatabaseRoot,

        [Parameter(Mandatory)]
        [string]$TargetVersion,

        [Parameter(Mandatory)]
        [string]$ScannerVersion,

        [int]$ProgressId = 40,

        [switch]$DisableProgress
    )

    $modLookup = Get-DatabaseModLookup -Mods @($Mods)
    $versionLookup = Get-DatabaseVersionLookup `
        -VersionStatus $VersionStatus

    $evidenceResults = @(
        Get-DatabaseSafeProperty `
            -InputObject $EvidenceScan `
            -PropertyName "Results" `
            -DefaultValue @()
    )

    $rulesFingerprint = Get-DatabaseSafeProperty `
        -InputObject $EvidenceScan `
        -PropertyName "RulesFingerprint"

    $packageIndex = [ordered]@{}
    $workshopIndex = [ordered]@{}
    $categoryIndex = @{}
    $familyIndex = @{}
    $reviewItems = @()
    $quarantineItems = @()
    $runRecords = @()
    $writtenCount = 0
    $unchangedCount = 0
    $skippedCount = 0
    $total = @($evidenceResults).Count
    $position = 0

    foreach ($evidenceResult in @($evidenceResults)) {
        $position++

        $packageId = ConvertTo-DatabasePackageId `
            -PackageId ([string](Get-DatabaseSafeProperty `
                -InputObject $evidenceResult `
                -PropertyName "PackageId" `
                -DefaultValue ""))

        if ($null -eq $packageId) {
            $skippedCount++
            continue
        }

        if ($packageId -match '^\(missing-package-id-\d+\)$') {
            $rootPath = [string](Get-DatabaseSafeProperty `
                -InputObject $evidenceResult `
                -PropertyName "RootPath" `
                -DefaultValue "")

            $workshopId = $null

            if ($rootPath -match '[\\/]294100[\\/](\d+)([\\/]|$)') {
                $workshopId = $Matches[1]
            }

            $quarantineItems += [PSCustomObject][ordered]@{
                Reason     = "MissingPackageId"
                Placeholder = $packageId
                Name       = Get-DatabaseSafeProperty `
                    -InputObject $evidenceResult `
                    -PropertyName "Name"
                WorkshopId = $workshopId
                ScanError  = Get-DatabaseSafeProperty `
                    -InputObject $evidenceResult `
                    -PropertyName "ScanError"
            }

            # Remove the legacy placeholder record created by earlier builds.
            $legacyPath = Get-DatabaseRecordPath `
                -DatabaseRoot $DatabaseRoot `
                -WorkshopId $null `
                -PackageId $packageId

            if (Test-Path -LiteralPath $legacyPath -PathType Leaf) {
                Remove-Item `
                    -LiteralPath $legacyPath `
                    -Force `
                    -ErrorAction SilentlyContinue
            }

            $skippedCount++
            continue
        }

        if (-not $DisableProgress) {
            $percent = if ($total -gt 0) {
                [Math]::Floor((($position - 1) / $total) * 100)
            }
            else {
                100
            }

            Write-Progress `
                -Id $ProgressId `
                -Activity "Building GitHub-ready mod database" `
                -Status ("[{0}/{1}] {2}" -f $position, $total, $packageId) `
                -PercentComplete $percent
        }

        $mod = if ($modLookup.ContainsKey($packageId)) {
            $modLookup[$packageId]
        }
        else {
            $null
        }

        $status = if ($versionLookup.ContainsKey($packageId)) {
            $versionLookup[$packageId]
        }
        else {
            $null
        }

        $published = New-PublishedModRecord `
            -EvidenceResult $evidenceResult `
            -Mod $mod `
            -VersionStatus $status `
            -TaxonomyDatabase $TaxonomyDatabase `
            -TargetVersion $TargetVersion `
            -ScannerVersion $ScannerVersion `
            -RulesFingerprint $rulesFingerprint

        if ($null -eq $published) {
            $skippedCount++
            continue
        }

        $record = $published.Record
        $workshopId = [string](Get-DatabaseSafeProperty `
            -InputObject $record.Identity `
            -PropertyName "WorkshopId" `
            -DefaultValue "")

        $relativePath = Get-DatabaseRecordPath `
            -DatabaseRoot $DatabaseRoot `
            -WorkshopId $workshopId `
            -PackageId $packageId

        $recordWithHash = [PSCustomObject][ordered]@{
            RecordHash = $published.RecordHash
            Data       = $record
        }

        $json = $recordWithHash |
            ConvertTo-Json -Depth 24

        $changed = Set-DatabaseFileIfChanged `
            -Path $relativePath `
            -Content $json

        if ($changed) {
            $writtenCount++
        }
        else {
            $unchangedCount++
        }

        $recordPathRelative = $relativePath.Substring(
            $DatabaseRoot.Length
        ).TrimStart(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar
        ).Replace("\", "/")

        $packageIndex[$packageId] = $recordPathRelative

        if (-not [string]::IsNullOrWhiteSpace($workshopId)) {
            $workshopIndex[$workshopId] = $recordPathRelative
        }

        $taxonomy = $record.Taxonomy
        $family = if ($null -ne $taxonomy) {
            [string](Get-DatabaseSafeProperty `
                -InputObject $taxonomy `
                -PropertyName "Family" `
                -DefaultValue "")
        }
        else {
            ""
        }

        $category = if ($null -ne $taxonomy) {
            [string](Get-DatabaseSafeProperty `
                -InputObject $taxonomy `
                -PropertyName "Category" `
                -DefaultValue "")
        }
        else {
            ""
        }

        if (-not [string]::IsNullOrWhiteSpace($family)) {
            if (-not $familyIndex.ContainsKey($family)) {
                $familyIndex[$family] = @()
            }

            $familyIndex[$family] = @($familyIndex[$family]) + $packageId
        }

        if (-not [string]::IsNullOrWhiteSpace($category)) {
            if (-not $categoryIndex.ContainsKey($category)) {
                $categoryIndex[$category] = @()
            }

            $categoryIndex[$category] =
                @($categoryIndex[$category]) + $packageId
        }

        $needsReview = [bool](Get-DatabaseSafeProperty `
            -InputObject $record.Evidence `
            -PropertyName "NeedsReview" `
            -DefaultValue $true)

        if ($needsReview) {
            $reviewItems += [PSCustomObject][ordered]@{
                PackageId  = $packageId
                WorkshopId = if (
                    [string]::IsNullOrWhiteSpace($workshopId)
                ) {
                    $null
                }
                else {
                    $workshopId
                }
                RecordPath = $recordPathRelative
                Suggested  = Get-DatabaseSafeProperty `
                    -InputObject $record.Evidence `
                    -PropertyName "SuggestedPrimary"
                Confidence = Get-DatabaseSafeProperty `
                    -InputObject $record.Evidence `
                    -PropertyName "Confidence" `
                    -DefaultValue 0
            }
        }

        $runRecords += [PSCustomObject][ordered]@{
            PackageId  = $packageId
            WorkshopId = if (
                [string]::IsNullOrWhiteSpace($workshopId)
            ) {
                $null
            }
            else {
                $workshopId
            }
            RecordHash = $published.RecordHash
            RecordPath = $recordPathRelative
        }
    }

    foreach ($key in @($familyIndex.Keys)) {
        $familyIndex[$key] = @(
            $familyIndex[$key] |
            Sort-Object -Unique
        )
    }

    foreach ($key in @($categoryIndex.Keys)) {
        $categoryIndex[$key] = @(
            $categoryIndex[$key] |
            Sort-Object -Unique
        )
    }

    $indexesRoot = Join-Path $DatabaseRoot "Indexes"

    $indexFiles = @{
        "PackageIds.json" = [PSCustomObject][ordered]@{
            SchemaVersion = 1
            Items         = $packageIndex
        }
        "WorkshopIds.json" = [PSCustomObject][ordered]@{
            SchemaVersion = 1
            Items         = $workshopIndex
        }
        "Categories.json" = [PSCustomObject][ordered]@{
            SchemaVersion = 1
            Items         = $categoryIndex
        }
        "Families.json" = [PSCustomObject][ordered]@{
            SchemaVersion = 1
            Items         = $familyIndex
        }
        "NeedsReview.json" = [PSCustomObject][ordered]@{
            SchemaVersion = 1
            Items         = @(
                $reviewItems |
                Sort-Object PackageId
            )
        }
        "Quarantine.json" = [PSCustomObject][ordered]@{
            SchemaVersion = 1
            Items         = @(
                $quarantineItems |
                Sort-Object WorkshopId, Placeholder
            )
        }
    }

    foreach ($name in @($indexFiles.Keys)) {
        $content = $indexFiles[$name] |
            ConvertTo-Json -Depth 16

        [void](Set-DatabaseFileIfChanged `
            -Path (Join-Path $indexesRoot $name) `
            -Content $content)
    }

    $runIdSource = @(
        $runRecords |
        Sort-Object PackageId |
        ConvertTo-Json -Depth 8 -Compress
    ) -join ""

    $manifest = [PSCustomObject][ordered]@{
        SchemaVersion     = 1
        DatabaseName      = "RimForge Generated Mod Database"
        TargetVersion     = $TargetVersion
        ScannerVersion    = $ScannerVersion
        BuildId           = Get-DatabaseSha256 -Text $runIdSource
        BuiltAtUtc        = [DateTime]::UtcNow.ToString("o")
        CurrentRun        = [PSCustomObject][ordered]@{
            InputModCount  = @($Mods).Count
            RecordCount    = @($runRecords).Count
            WrittenCount   = $writtenCount
            UnchangedCount = $unchangedCount
            SkippedCount   = $skippedCount
            ReviewCount    = @($reviewItems).Count
            QuarantineCount = @($quarantineItems).Count
        }
        Records           = @(
            $runRecords |
            Sort-Object PackageId
        )
    }

    [void](Set-DatabaseFileIfChanged `
        -Path (Join-Path $DatabaseRoot "Manifest.json") `
        -Content ($manifest | ConvertTo-Json -Depth 12))

    Write-DatabaseSupportFiles -DatabaseRoot $DatabaseRoot

    if (-not $DisableProgress) {
        Write-Progress `
            -Id $ProgressId `
            -Activity "Building GitHub-ready mod database" `
            -Status "Database build complete." `
            -PercentComplete 100 `
            -Completed
    }

    return [PSCustomObject]@{
        DatabaseRoot    = $DatabaseRoot
        BuildId         = $manifest.BuildId
        RecordCount     = @($runRecords).Count
        WrittenCount    = $writtenCount
        UnchangedCount  = $unchangedCount
        SkippedCount    = $skippedCount
        ReviewCount     = @($reviewItems).Count
        QuarantineCount = @($quarantineItems).Count
        ManifestPath    = Join-Path $DatabaseRoot "Manifest.json"
        PackageIndex    = Join-Path $indexesRoot "PackageIds.json"
        WorkshopIndex   = Join-Path $indexesRoot "WorkshopIds.json"
        ReviewIndex     = Join-Path $indexesRoot "NeedsReview.json"
        QuarantineIndex = Join-Path $indexesRoot "Quarantine.json"
    }
}

function Test-RimForgeGeneratedDatabase {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$DatabaseRoot
    )

    $errors = @()
    $warnings = @()
    $manifestPath = Join-Path $DatabaseRoot "Manifest.json"

    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        $errors += "Manifest.json is missing."
    }

    $packageIndexPath = Join-Path `
        $DatabaseRoot `
        "Indexes\PackageIds.json"

    if (-not (Test-Path -LiteralPath $packageIndexPath -PathType Leaf)) {
        $errors += "Indexes\\PackageIds.json is missing."
    }
    else {
        try {
            $packageIndex = Get-Content `
                -LiteralPath $packageIndexPath `
                -Raw |
                ConvertFrom-Json

            foreach ($property in @($packageIndex.Items.PSObject.Properties)) {
                $recordPath = Join-Path `
                    $DatabaseRoot `
                    ([string]$property.Value).Replace("/", "\")

                if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) {
                    $errors += (
                        "Missing record for package {0}: {1}" -f
                        $property.Name,
                        $property.Value
                    )
                    continue
                }

                try {
                    $record = Get-Content `
                        -LiteralPath $recordPath `
                        -Raw |
                        ConvertFrom-Json

                    $bodyJson = ConvertTo-CanonicalJson `
                        -InputObject $record.Data `
                        -Depth 24

                    $actualHash = Get-DatabaseSha256 -Text $bodyJson

                    if ([string]$record.RecordHash -ne $actualHash) {
                        $errors += (
                            "Hash mismatch for {0}." -f
                            $property.Name
                        )
                    }
                }
                catch {
                    $errors += (
                        "Invalid record JSON for {0}: {1}" -f
                        $property.Name,
                        $_.Exception.Message
                    )
                }
            }
        }
        catch {
            $errors += (
                "Invalid package index JSON: {0}" -f
                $_.Exception.Message
            )
        }
    }

    return [PSCustomObject]@{
        IsValid      = (@($errors).Count -eq 0)
        ErrorCount   = @($errors).Count
        WarningCount = @($warnings).Count
        Errors       = @($errors)
        Warnings     = @($warnings)
    }
}

Export-ModuleMember -Function `
    Export-RimForgeGeneratedDatabase,
    Test-RimForgeGeneratedDatabase
