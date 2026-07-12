Set-StrictMode -Version Latest

function Get-EvidenceSafeProperty {
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

function Get-EvidencePropertyNames {
    [CmdletBinding()]
    param(
        [AllowNull()]
        $InputObject
    )

    if ($null -eq $InputObject) {
        return @()
    }

    $names = @()

    foreach ($property in @($InputObject.PSObject.Properties)) {
        if (
            $null -ne $property -and
            -not [string]::IsNullOrWhiteSpace([string]$property.Name)
        ) {
            $names += [string]$property.Name
        }
    }

    return @($names)
}

function Add-EvidenceCount {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Table,

        [Parameter(Mandatory)]
        [string]$Key,

        [int]$Amount = 1
    )

    if ([string]::IsNullOrWhiteSpace($Key)) {
        return
    }

    if (-not $Table.ContainsKey($Key)) {
        $Table[$Key] = 0
    }

    $Table[$Key] += $Amount
}

function Import-EvidenceRules {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Evidence rules not found: $Path"
    }

    try {
        $data = Get-Content -LiteralPath $Path -Raw |
            ConvertFrom-Json
    }
    catch {
        throw "Evidence rules JSON is invalid. $($_.Exception.Message)"
    }

    return [PSCustomObject]@{
        SchemaVersion          = $data.SchemaVersion
        MinimumPrimaryScore    = [int]$data.MinimumPrimaryScore
        MinimumSecondaryScore  = [int]$data.MinimumSecondaryScore
        ReviewBelowConfidence  = [int]$data.ReviewBelowConfidence
        Categories             = @($data.Categories)
        SourcePath             = $Path
    }
}

function Get-ModRootPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Mod
    )

    foreach ($name in @("RootPath", "Path", "FolderPath", "ModPath")) {
        $value = Get-EvidenceSafeProperty `
            -InputObject $Mod `
            -PropertyName $name

        if (
            $null -ne $value -and
            -not [string]::IsNullOrWhiteSpace([string]$value) -and
            (Test-Path -LiteralPath ([string]$value) -PathType Container)
        ) {
            return [string]$value
        }
    }

    return $null
}

function Read-XmlEvidenceFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [hashtable]$DefTypes,

        [Parameter(Mandatory)]
        [hashtable]$ElementNames,

        [Parameter(Mandatory)]
        [hashtable]$ClassNames,

        [Parameter(Mandatory)]
        [hashtable]$Errors
    )

    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.IgnoreComments = $true
    $settings.IgnoreWhitespace = $true

    $reader = $null

    try {
        $reader = [System.Xml.XmlReader]::Create($Path, $settings)

        while ($reader.Read()) {
            if ($reader.NodeType -ne [System.Xml.XmlNodeType]::Element) {
                continue
            }

            $name = [string]$reader.LocalName
            Add-EvidenceCount -Table $ElementNames -Key $name

            if ($name -match 'Def$') {
                Add-EvidenceCount -Table $DefTypes -Key $name
            }

            foreach ($attributeName in @(
                "Class",
                "class",
                "MayRequire",
                "MayRequireAnyOf"
            )) {
                $attributeValue = $reader.GetAttribute($attributeName)

                if (-not [string]::IsNullOrWhiteSpace($attributeValue)) {
                    foreach ($part in @($attributeValue -split '[,; ]+')) {
                        if (-not [string]::IsNullOrWhiteSpace($part)) {
                            Add-EvidenceCount -Table $ClassNames -Key $part
                        }
                    }
                }
            }
        }
    }
    catch {
        Add-EvidenceCount `
            -Table $Errors `
            -Key ("{0}: {1}" -f $Path, $_.Exception.Message)
    }
    finally {
        if ($null -ne $reader) {
            $reader.Dispose()
        }
    }
}

function Get-AssemblyStaticEvidence {
    <#
    .DESCRIPTION
        Reads DLL bytes as text-like metadata and searches for identifier
        patterns. It does not load or execute the assembly.
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [array]$Patterns
    )

    $hits = @()

    try {
        $bytes = [System.IO.File]::ReadAllBytes($Path)
        $ascii = [System.Text.Encoding]::ASCII.GetString($bytes)
        $unicode = [System.Text.Encoding]::Unicode.GetString($bytes)

        foreach ($pattern in @($Patterns)) {
            if (
                $ascii -match [string]$pattern -or
                $unicode -match [string]$pattern
            ) {
                $hits += [string]$pattern
            }
        }
    }
    catch {
        return [PSCustomObject]@{
            Path  = $Path
            Hits  = @()
            Error = $_.Exception.Message
        }
    }

    return [PSCustomObject]@{
        Path  = $Path
        Hits  = @($hits | Sort-Object -Unique)
        Error = $null
    }
}

function Get-AllRulePatterns {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Rules,

        [Parameter(Mandatory)]
        [string]$SignalProperty
    )

    $patterns = @()

    foreach ($category in @($Rules.Categories)) {
        $signals = Get-EvidenceSafeProperty `
            -InputObject $category `
            -PropertyName "Signals"

        $map = Get-EvidenceSafeProperty `
            -InputObject $signals `
            -PropertyName $SignalProperty

        if ($null -eq $map) {
            continue
        }

        foreach ($property in @($map.PSObject.Properties)) {
            $patterns += [string]$property.Name
        }
    }

    return @($patterns | Sort-Object -Unique)
}

function Test-PatternAgainstValues {
    [CmdletBinding()]
    param(
        [AllowNull()]
        $PatternMap,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [string[]]$Values,

        [Parameter(Mandatory)]
        [string]$EvidenceType
    )

    $score = 0
    $evidence = @()

    if ($null -eq $PatternMap) {
        return [PSCustomObject]@{
            Score    = 0
            Evidence = @()
        }
    }

    foreach ($property in @($PatternMap.PSObject.Properties)) {
        $pattern = [string]$property.Name
        $weight = [int]$property.Value
        $matchingValues = @(
            $Values |
            Where-Object { [string]$_ -match $pattern } |
            Select-Object -First 5
        )

        if (@($matchingValues).Count -gt 0) {
            $score += $weight
            $evidence += [PSCustomObject]@{
                Type    = $EvidenceType
                Pattern = $pattern
                Weight  = $weight
                Matches = @($matchingValues)
            }
        }
    }

    return [PSCustomObject]@{
        Score    = $score
        Evidence = @($evidence)
    }
}

function Get-DefTypeScore {
    [CmdletBinding()]
    param(
        [AllowNull()]
        $DefTypeMap,

        [Parameter(Mandatory)]
        [hashtable]$DefTypes
    )

    $score = 0
    $evidence = @()

    if ($null -eq $DefTypeMap) {
        return [PSCustomObject]@{
            Score    = 0
            Evidence = @()
        }
    }

    foreach ($property in @($DefTypeMap.PSObject.Properties)) {
        $defType = [string]$property.Name
        $weight = [int]$property.Value

        if ($DefTypes.ContainsKey($defType) -and $DefTypes[$defType] -gt 0) {
            $score += $weight
            $evidence += [PSCustomObject]@{
                Type    = "XmlDefType"
                DefType = $defType
                Count   = $DefTypes[$defType]
                Weight  = $weight
            }
        }
    }

    return [PSCustomObject]@{
        Score    = $score
        Evidence = @($evidence)
    }
}

function Get-EvidenceClassification {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $RawEvidence,

        [Parameter(Mandatory)]
        $Rules
    )

    $results = @()

    $xmlClassObject = Get-EvidenceSafeProperty `
        -InputObject $RawEvidence `
        -PropertyName "XmlClassNames"

    $classValues = @(
        Get-EvidencePropertyNames `
            -InputObject $xmlClassObject
    )

    $pathValues = @(
        Get-EvidenceSafeProperty `
            -InputObject $RawEvidence `
            -PropertyName "RelativePaths" `
            -DefaultValue @()
    )

    $dependencyValues = @(
        Get-EvidenceSafeProperty `
            -InputObject $RawEvidence `
            -PropertyName "Dependencies" `
            -DefaultValue @()
    )

    $assemblyValues = @(
        Get-EvidenceSafeProperty `
            -InputObject $RawEvidence `
            -PropertyName "AssemblyPatternHits" `
            -DefaultValue @()
    )

    $defTypeTable = @{}

    $xmlDefTypeObject = Get-EvidenceSafeProperty `
        -InputObject $RawEvidence `
        -PropertyName "XmlDefTypes"

    if ($null -ne $xmlDefTypeObject) {
        foreach ($property in @($xmlDefTypeObject.PSObject.Properties)) {
            if ($null -eq $property) {
                continue
            }

            $defTypeTable[[string]$property.Name] = [int]$property.Value
        }
    }

    foreach ($category in @($Rules.Categories)) {
        $signals = $category.Signals

        $defResult = Get-DefTypeScore `
            -DefTypeMap (Get-EvidenceSafeProperty $signals "XmlDefTypes") `
            -DefTypes $defTypeTable

        $classResult = Test-PatternAgainstValues `
            -PatternMap (Get-EvidenceSafeProperty $signals "XmlClassPatterns") `
            -Values $classValues `
            -EvidenceType "XmlClass"

        $assemblyResult = Test-PatternAgainstValues `
            -PatternMap (Get-EvidenceSafeProperty $signals "AssemblyPatterns") `
            -Values $assemblyValues `
            -EvidenceType "AssemblyIdentifier"

        $pathResult = Test-PatternAgainstValues `
            -PatternMap (Get-EvidenceSafeProperty $signals "PathPatterns") `
            -Values $pathValues `
            -EvidenceType "FilePath"

        $dependencyResult = Test-PatternAgainstValues `
            -PatternMap (Get-EvidenceSafeProperty $signals "DependencyPatterns") `
            -Values $dependencyValues `
            -EvidenceType "Dependency"

        $score = (
            $defResult.Score +
            $classResult.Score +
            $assemblyResult.Score +
            $pathResult.Score +
            $dependencyResult.Score
        )

        $results += [PSCustomObject]@{
            CategoryId   = [string]$category.Id
            DisplayName  = [string]$category.DisplayName
            Score        = [int]$score
            Evidence     = @(
                $defResult.Evidence
                $classResult.Evidence
                $assemblyResult.Evidence
                $pathResult.Evidence
                $dependencyResult.Evidence
            )
        }
    }

    $ranked = @(
        $results |
        Sort-Object `
            @{ Expression = { $_.Score }; Descending = $true },
            @{ Expression = { $_.DisplayName } }
    )

    $top = @($ranked | Select-Object -First 1)[0]
    $second = @($ranked | Select-Object -Skip 1 -First 1)

    $primary = $null
    $confidence = 0

    if ($null -ne $top -and $top.Score -ge $Rules.MinimumPrimaryScore) {
        $primary = $top
        $secondScore = if (@($second).Count -gt 0) {
            [int]$second[0].Score
        }
        else {
            0
        }

        $margin = [Math]::Max(0, $top.Score - $secondScore)
        $confidence = [Math]::Min(
            99,
            [Math]::Round(
                45 +
                ([Math]::Min($top.Score, 20) * 2) +
                ([Math]::Min($margin, 10) * 2)
            )
        )
    }

    $secondary = @(
        $ranked |
        Where-Object {
            $_.Score -ge $Rules.MinimumSecondaryScore -and
            ($null -eq $primary -or $_.CategoryId -ne $primary.CategoryId)
        } |
        Select-Object -First 4
    )

    return [PSCustomObject]@{
        PrimaryCategory = if ($null -ne $primary) {
            $primary.DisplayName
        }
        else {
            "Unclassified"
        }
        PrimaryCategoryId = if ($null -ne $primary) {
            $primary.CategoryId
        }
        else {
            $null
        }
        Confidence       = [int]$confidence
        NeedsReview      = (
            $null -eq $primary -or
            $confidence -lt $Rules.ReviewBelowConfidence
        )
        Secondary        = @($secondary)
        RankedScores     = @($ranked)
    }
}

function Convert-HashtableToObject {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Table
    )

    $ordered = [ordered]@{}

    foreach ($key in @($Table.Keys | Sort-Object)) {
        $ordered[$key] = $Table[$key]
    }

    return [PSCustomObject]$ordered
}

function Get-ModDependenciesForEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Mod
    )

    foreach ($propertyName in @(
        "Dependencies",
        "ModDependencies",
        "RequiredPackageIds"
    )) {
        $value = Get-EvidenceSafeProperty `
            -InputObject $Mod `
            -PropertyName $propertyName

        if ($null -eq $value) {
            continue
        }

        $results = @()

        foreach ($item in @($value)) {
            if ($null -eq $item) {
                continue
            }

            if ($item -is [string]) {
                $results += [string]$item
                continue
            }

            foreach ($name in @("PackageId", "PackageID", "packageId")) {
                if ($item.PSObject.Properties.Name -contains $name) {
                    $results += [string]$item.$name
                    break
                }
            }
        }

        return @(
            $results |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object { $_.Trim().ToLowerInvariant() } |
            Sort-Object -Unique
        )
    }

    return @()
}

function Get-EvidenceRulesFingerprint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Rules
    )

    $serialized = $Rules |
        ConvertTo-Json -Depth 20 -Compress

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($serialized)
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

function Get-EvidenceFileSignature {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RootPath,

        [Parameter(Mandatory)]
        [array]$Files,

        [Parameter(Mandatory)]
        [string]$RulesFingerprint
    )

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine("schema=1")
    [void]$builder.AppendLine("rules=$RulesFingerprint")

    foreach ($file in @(
        $Files |
        Sort-Object FullName
    )) {
        $relative = $file.FullName.Substring($RootPath.Length).TrimStart(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar
        )

        [void]$builder.Append($relative.ToLowerInvariant())
        [void]$builder.Append("|")
        [void]$builder.Append([string]$file.Length)
        [void]$builder.Append("|")
        [void]$builder.AppendLine(
            [string]$file.LastWriteTimeUtc.Ticks
        )
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes(
        $builder.ToString()
    )
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


function Get-EvidenceIndexPath {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CacheFolder)
    return Join-Path $CacheFolder 'EvidenceIndex.json'
}

function Read-EvidenceIndex {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ExpectedRulesFingerprint
    )
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try {
        $index = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        if ([string]$index.RulesFingerprint -ne $ExpectedRulesFingerprint) { return $null }
        if ($null -eq $index.Results) { return $null }
        return $index
    }
    catch { return $null }
}

function Write-EvidenceIndex {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$RulesFingerprint,
        [Parameter(Mandatory)][array]$Results
    )
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $temporaryPath = "{0}.{1}.tmp" -f $Path,[guid]::NewGuid().ToString('N')
    try {
        [PSCustomObject]@{
            SchemaVersion = 1
            RulesFingerprint = $RulesFingerprint
            WrittenUtc = [DateTime]::UtcNow.ToString('o')
            Results = @($Results)
        } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    }
    finally { Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue }
}

function Get-EvidenceCachePath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$CacheFolder,

        [Parameter(Mandatory)]
        [string]$PackageId
    )

    $safeName = $PackageId -replace '[^a-zA-Z0-9._-]', "_"

    if ([string]::IsNullOrWhiteSpace($safeName)) {
        $safeName = "unknown-mod"
    }

    return Join-Path $CacheFolder ("{0}.json" -f $safeName)
}

function Read-EvidenceCacheItem {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$ExpectedSignature
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    try {
        $cache = Get-Content -LiteralPath $Path -Raw |
            ConvertFrom-Json

        if (
            [string]$cache.Signature -ne $ExpectedSignature -or
            $null -eq $cache.Result
        ) {
            return $null
        }

        return $cache.Result
    }
    catch {
        return $null
    }
}

function Write-EvidenceCacheItem {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Signature,

        [Parameter(Mandatory)]
        $Result
    )

    $parent = Split-Path -Parent $Path

    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force |
            Out-Null
    }

    $temporaryPath = "{0}.{1}.tmp" -f $Path, [guid]::NewGuid().ToString("N")

    try {
        [PSCustomObject]@{
            SchemaVersion = 1
            Signature     = $Signature
            CachedAtUtc   = [DateTime]::UtcNow.ToString("o")
            Result        = $Result
        } |
            ConvertTo-Json -Depth 20 |
            Set-Content -LiteralPath $temporaryPath -Encoding UTF8

        Move-Item `
            -LiteralPath $temporaryPath `
            -Destination $Path `
            -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }
    }
}


function Test-EvidencePathExcluded {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RelativePath
    )

    $segments = @(
        $RelativePath -split '[\\/]'
    )

    $excludedNames = @(
        ".git",
        ".github",
        ".vs",
        ".idea",
        "Source",
        "Sources",
        "Tests",
        "Test",
        "Docs",
        "Documentation",
        "obj",
        "bin",
        "Build",
        "Builds",
        "Cache",
        "Caches",
        "Output",
        "Logs",
        "Packages",
        "NuGet"
    )

    foreach ($segment in @($segments)) {
        if ($excludedNames -contains $segment) {
            return $true
        }
    }

    return $false
}

function Test-EvidencePathForTargetVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RelativePath,

        [Parameter(Mandatory)]
        [string]$TargetVersion
    )

    $firstSegment = @(
        $RelativePath -split '[\\/]'
    )[0]

    if ($firstSegment -match '^(?i)v?(\d+\.\d+)$') {
        return ($Matches[1] -eq $TargetVersion)
    }

    # Root, Common, About, and other non-versioned content is shared.
    return $true
}

function Test-EvidenceAssemblyCandidate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.IO.FileInfo]$File
    )

    $excludedAssemblyNames = @(
        "0Harmony.dll",
        "Harmony.dll",
        "HugsLib.dll",
        "Mono.Cecil.dll",
        "Mono.Cecil.Pdb.dll",
        "Mono.Cecil.Mdb.dll",
        "Newtonsoft.Json.dll",
        "Assembly-CSharp.dll",
        "UnityEngine.dll"
    )

    return ($excludedAssemblyNames -notcontains $File.Name)
}

function Invoke-ModEvidenceScan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Mods,

        [Parameter(Mandatory)]
        $Rules,

        [Parameter(Mandatory)]
        [string]$OutputFolder,

        [int]$ProgressId = 20,

        [switch]$DisableProgress,

        [string]$CacheFolder,

        [bool]$UseCache = $true,

        [switch]$ForceRescan,

        [string[]]$TrustedUnchangedPackageIds = @(),

        [string]$TargetVersion = "1.6"
    )

    $assemblyPatterns = Get-AllRulePatterns `
        -Rules $Rules `
        -SignalProperty "AssemblyPatterns"

    $results = @()
    $index = 0
    $totalMods = @($Mods).Count
    $cacheHitCount = 0
    $scannedCount = 0
    $cacheWriteErrorCount = 0
    $rulesFingerprint = (
        "{0}|target={1}|scanner=2" -f
        (Get-EvidenceRulesFingerprint -Rules $Rules),
        $TargetVersion
    )

    if ([string]::IsNullOrWhiteSpace($CacheFolder)) {
        $CacheFolder = Join-Path $OutputFolder "..\Cache\Evidence"
    }

    if (
        $UseCache -and
        -not (Test-Path -LiteralPath $CacheFolder -PathType Container)
    ) {
        New-Item -ItemType Directory -Path $CacheFolder -Force |
            Out-Null
    }

    $trustedSet = @{}
    foreach ($trustedId in @($TrustedUnchangedPackageIds)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$trustedId)) {
            $trustedSet[[string]$trustedId.ToLowerInvariant()] = $true
        }
    }

    $evidenceIndexPath = Get-EvidenceIndexPath -CacheFolder $CacheFolder
    $evidenceIndex = if ($UseCache -and -not $ForceRescan) {
        Read-EvidenceIndex -Path $evidenceIndexPath -ExpectedRulesFingerprint $rulesFingerprint
    }
    else { $null }
    $indexedByPackage = @{}
    if ($null -ne $evidenceIndex) {
        foreach ($indexedResult in @($evidenceIndex.Results)) {
            if ($null -ne $indexedResult -and -not [string]::IsNullOrWhiteSpace([string]$indexedResult.PackageId)) {
                $indexedByPackage[[string]$indexedResult.PackageId.ToLowerInvariant()] = $indexedResult
            }
        }
    }

    if (-not $DisableProgress) {
        Write-Progress `
            -Id $ProgressId `
            -Activity "Scanning mod evidence" `
            -Status "Preparing to scan $totalMods mod(s)..." `
            -PercentComplete 0
    }

    foreach ($mod in @($Mods)) {
        $index++

        $progressName = [string](Get-EvidenceSafeProperty `
            -InputObject $mod `
            -PropertyName "Name" `
            -DefaultValue ("Mod {0}" -f $index))

        if (-not $DisableProgress) {
            $percent = if ($totalMods -gt 0) {
                [Math]::Min(
                    99,
                    [Math]::Floor((($index - 1) / $totalMods) * 100)
                )
            }
            else {
                0
            }

            Write-Progress `
                -Id $ProgressId `
                -Activity "Scanning mod evidence" `
                -Status ("[{0}/{1}] {2}" -f $index, $totalMods, $progressName) `
                -CurrentOperation "Reading XML, paths, and static DLL identifiers" `
                -PercentComplete $percent
        }

        $rootPath = Get-ModRootPath -Mod $mod
        $packageId = [string](Get-EvidenceSafeProperty `
            -InputObject $mod `
            -PropertyName "PackageId" `
            -DefaultValue "")

        $name = [string](Get-EvidenceSafeProperty `
            -InputObject $mod `
            -PropertyName "Name" `
            -DefaultValue $packageId)

        if ([string]::IsNullOrWhiteSpace($packageId)) {
            $packageId = "(missing-package-id-$index)"
        }

        $packageKey = $packageId.ToLowerInvariant()
        if (
            $UseCache -and
            -not $ForceRescan -and
            $trustedSet.ContainsKey($packageKey) -and
            $indexedByPackage.ContainsKey($packageKey)
        ) {
            $cachedResult = $indexedByPackage[$packageKey]
            if ($cachedResult.PSObject.Properties.Name -contains 'EvidenceSource') {
                $cachedResult.EvidenceSource = 'CacheIndex'
            }
            else {
                $cachedResult | Add-Member -NotePropertyName 'EvidenceSource' -NotePropertyValue 'CacheIndex'
            }
            $results += $cachedResult
            $cacheHitCount++
            continue
        }

        if ($null -eq $rootPath) {
            $results += [PSCustomObject]@{
                PackageId = $packageId
                Name      = $name
                RootPath  = $null
                ScanError = "No valid mod root path was available."
                RawEvidence = $null
                Classification = [PSCustomObject]@{
                    PrimaryCategory = "Unclassified"
                    PrimaryCategoryId = $null
                    Confidence = 0
                    NeedsReview = $true
                    Secondary = @()
                    RankedScores = @()
                }
            }
            continue
        }

        $xmlDefTypes = @{}
        $xmlElementNames = @{}
        $xmlClassNames = @{}
        $xmlErrors = @{}
        $relativePaths = @()
        $xmlFiles = @()
        $dllFiles = @()

        try {
            $allFiles = @(
                Get-ChildItem `
                    -LiteralPath $rootPath `
                    -File `
                    -Recurse `
                    -ErrorAction SilentlyContinue
            )

            foreach ($file in @($allFiles)) {
                $relative = $file.FullName.Substring($rootPath.Length).TrimStart(
                    [System.IO.Path]::DirectorySeparatorChar,
                    [System.IO.Path]::AltDirectorySeparatorChar
                )

                if (Test-EvidencePathExcluded -RelativePath $relative) {
                    continue
                }

                if (-not (Test-EvidencePathForTargetVersion `
                    -RelativePath $relative `
                    -TargetVersion $TargetVersion)) {
                    continue
                }

                $isEvidenceFile = $false

                if ($file.Extension -ieq ".xml") {
                    if (
                        $relative -match '^(?:(?:v?\d+\.\d+|Common)[\\/])?(Defs|Patches)[\\/]' -or
                        $relative -match '^(?:(?:v?\d+\.\d+|Common)[\\/])?About[\\/]About\.xml$'
                    ) {
                        $xmlFiles += $file
                        $isEvidenceFile = $true
                    }
                }
                elseif (
                    $file.Extension -ieq ".dll" -and
                    (Test-EvidenceAssemblyCandidate -File $file)
                ) {
                    $dllFiles += $file
                    $isEvidenceFile = $true
                }

                if ($isEvidenceFile) {
                    $relativePaths += $relative
                }
            }
        }
        catch {
            Add-EvidenceCount `
                -Table $xmlErrors `
                -Key ("File enumeration: {0}" -f $_.Exception.Message)
        }

        $signatureFiles = @(
            $xmlFiles
            $dllFiles
        )

        $signature = Get-EvidenceFileSignature `
            -RootPath $rootPath `
            -Files @($signatureFiles) `
            -RulesFingerprint $rulesFingerprint

        $cachePath = Get-EvidenceCachePath `
            -CacheFolder $CacheFolder `
            -PackageId $packageId

        if ($UseCache -and -not $ForceRescan) {
            $cachedResult = Read-EvidenceCacheItem `
                -Path $cachePath `
                -ExpectedSignature $signature

            if ($null -ne $cachedResult) {
                if (
                    -not (
                        $cachedResult.PSObject.Properties.Name -contains
                        "EvidenceSource"
                    )
                ) {
                    $cachedResult |
                        Add-Member `
                            -NotePropertyName "EvidenceSource" `
                            -NotePropertyValue "Cache"
                }
                else {
                    $cachedResult.EvidenceSource = "Cache"
                }

                $results += $cachedResult
                $cacheHitCount++

                if (-not $DisableProgress) {
                    $percent = if ($totalMods -gt 0) {
                        [Math]::Min(
                            99,
                            [Math]::Floor(($index / $totalMods) * 100)
                        )
                    }
                    else {
                        100
                    }

                    Write-Progress `
                        -Id $ProgressId `
                        -Activity "Scanning mod evidence" `
                        -Status (
                            "[{0}/{1}] {2} (cached: {3}, scanned: {4})" -f
                            $index,
                            $totalMods,
                            $progressName,
                            $cacheHitCount,
                            $scannedCount
                        ) `
                        -CurrentOperation "Loaded unchanged evidence from cache" `
                        -PercentComplete $percent
                }

                continue
            }
        }

        $scannedCount++

        foreach ($file in @($xmlFiles)) {
            Read-XmlEvidenceFile `
                -Path $file.FullName `
                -DefTypes $xmlDefTypes `
                -ElementNames $xmlElementNames `
                -ClassNames $xmlClassNames `
                -Errors $xmlErrors
        }

        $assemblyHits = @()
        $assemblyErrors = @()

        foreach ($file in @($dllFiles)) {
            $assemblyResult = Get-AssemblyStaticEvidence `
                -Path $file.FullName `
                -Patterns $assemblyPatterns

            foreach ($hit in @($assemblyResult.Hits)) {
                $assemblyHits += $hit
            }

            if (-not [string]::IsNullOrWhiteSpace($assemblyResult.Error)) {
                $assemblyErrors += [PSCustomObject]@{
                    Path  = $file.FullName
                    Error = $assemblyResult.Error
                }
            }
        }

        $raw = [PSCustomObject]@{
            XmlFileCount       = @($xmlFiles).Count
            AssemblyFileCount  = @($dllFiles).Count
            RelativePaths      = @($relativePaths)
            XmlDefTypes        = Convert-HashtableToObject $xmlDefTypes
            XmlElementNames    = Convert-HashtableToObject $xmlElementNames
            XmlClassNames      = Convert-HashtableToObject $xmlClassNames
            Dependencies       = @(
                Get-ModDependenciesForEvidence -Mod $mod
            )
            AssemblyPatternHits = @(
                $assemblyHits |
                Sort-Object -Unique
            )
            XmlErrors           = @($xmlErrors.Keys)
            AssemblyErrors      = @($assemblyErrors)
        }

        $classification = Get-EvidenceClassification `
            -RawEvidence $raw `
            -Rules $Rules

        $modResult = [PSCustomObject]@{
            PackageId      = $packageId
            Name           = $name
            RootPath       = $rootPath
            ScanError      = $null
            RawEvidence    = $raw
            Classification = $classification
            EvidenceSource = "Scanned"
        }

        $results += $modResult

        if ($UseCache) {
            try {
                Write-EvidenceCacheItem `
                    -Path $cachePath `
                    -Signature $signature `
                    -Result $modResult
            }
            catch {
                $cacheWriteErrorCount++
            }
        }
    }

    if (-not $DisableProgress) {
        Write-Progress `
            -Id $ProgressId `
            -Activity "Scanning mod evidence" `
            -Status ("Completed {0} mod(s)." -f $totalMods) `
            -PercentComplete 100 `
            -Completed
    }

    if ($UseCache) {
        try {
            Write-EvidenceIndex -Path $evidenceIndexPath -RulesFingerprint $rulesFingerprint -Results @($results)
        }
        catch {
            $cacheWriteErrorCount++
        }
    }

    if (-not (Test-Path -LiteralPath $OutputFolder -PathType Container)) {
        New-Item -ItemType Directory -Path $OutputFolder -Force |
            Out-Null
    }

    $reportPath = Join-Path $OutputFolder "EvidenceReport.json"
    $reviewPath = Join-Path $OutputFolder "ClassificationReview.json"
    $summaryPath = Join-Path $OutputFolder "EvidenceSummary.txt"

    $summary = [PSCustomObject]@{
        Generated             = (Get-Date).ToString("o")
        ModCount              = @($results).Count
        ClassifiedCount       = @(
            $results |
            Where-Object {
                $_.Classification.PrimaryCategory -ne "Unclassified"
            }
        ).Count
        ReviewCount           = @(
            $results |
            Where-Object { $_.Classification.NeedsReview }
        ).Count
        CacheEnabled          = $UseCache
        CacheFolder           = $CacheFolder
        CacheHitCount         = $cacheHitCount
        ScannedCount          = $scannedCount
        CacheWriteErrorCount  = $cacheWriteErrorCount
        RulesFingerprint      = $rulesFingerprint
        RulesPath             = $Rules.SourcePath
        Results               = @($results)
    }

    $summary |
        ConvertTo-Json -Depth 18 |
        Set-Content -LiteralPath $reportPath -Encoding UTF8

    @(
        $results |
        Where-Object { $_.Classification.NeedsReview } |
        ForEach-Object {
            [PSCustomObject]@{
                PackageId      = $_.PackageId
                Name           = $_.Name
                Suggested      = $_.Classification.PrimaryCategory
                Confidence     = $_.Classification.Confidence
                TopScores      = @(
                    $_.Classification.RankedScores |
                    Select-Object -First 5
                )
            }
        }
    ) |
        ConvertTo-Json -Depth 12 |
        Set-Content -LiteralPath $reviewPath -Encoding UTF8

    @(
        foreach ($item in @($results)) {
            "{0}`t{1}`t{2}%`tReview={3}" -f
            $item.PackageId,
            $item.Classification.PrimaryCategory,
            $item.Classification.Confidence,
            $item.Classification.NeedsReview
        }
    ) |
        Set-Content -LiteralPath $summaryPath -Encoding UTF8

    return [PSCustomObject]@{
        ModCount        = @($results).Count
        ClassifiedCount = $summary.ClassifiedCount
        ReviewCount     = $summary.ReviewCount
        CacheEnabled    = $UseCache
        CacheFolder     = $CacheFolder
        CacheHitCount   = $cacheHitCount
        ScannedCount    = $scannedCount
        CacheWriteErrorCount = $cacheWriteErrorCount
        Results         = @($results)
        ReportPath      = $reportPath
        ReviewPath      = $reviewPath
        SummaryPath     = $summaryPath
    }
}

Export-ModuleMember -Function `
    Import-EvidenceRules,
    Invoke-ModEvidenceScan
