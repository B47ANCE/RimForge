Set-StrictMode -Version Latest

function ConvertTo-TaxonomyPackageId {
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

function Import-ModTaxonomyDatabase {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$TaxonomyPath,

        [Parameter(Mandatory)]
        [string]$RulesPath,

        [Parameter(Mandatory)]
        [string]$FamilyRulesPath,

        [Parameter(Mandatory)]
        [string]$TargetVersion
    )

    foreach ($path in @($TaxonomyPath, $RulesPath, $FamilyRulesPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Taxonomy database file not found: $path"
        }
    }

    try {
        $taxonomy = Get-Content -LiteralPath $TaxonomyPath -Raw |
            ConvertFrom-Json
        $rules = Get-Content -LiteralPath $RulesPath -Raw |
            ConvertFrom-Json
        $familyRules = Get-Content -LiteralPath $FamilyRulesPath -Raw |
            ConvertFrom-Json
    }
    catch {
        throw "Taxonomy database JSON is invalid. $($_.Exception.Message)"
    }

    $entries = @(
        foreach ($entry in @($taxonomy.Mods)) {
            $versions = @($entry.AppliesToVersions)

            if (
                @($versions).Count -eq 0 -or
                $versions -contains $TargetVersion
            ) {
                $entry
            }
        }
    )

    $activeFamilyRules = @(
        foreach ($rule in @($familyRules.Rules)) {
            if (
                $rule.PSObject.Properties.Name -contains "Enabled" -and
                $rule.Enabled -eq $false
            ) {
                continue
            }

            $rule
        }
    )

    return [PSCustomObject]@{
        SchemaVersion     = 2
        AllowedCategories = @($taxonomy.AllowedCategories)
        AllowedRoles      = @($taxonomy.AllowedRoles)
        Entries           = @($entries)
        FamilyRules       = @($activeFamilyRules)
        RolePrecedence    = @($rules.RolePrecedence)
        TaxonomyPath      = $TaxonomyPath
        RulesPath         = $RulesPath
        FamilyRulesPath   = $FamilyRulesPath
        TargetVersion     = $TargetVersion
    }
}

function Get-InferredTaxonomyEntry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$PackageId,

        [Parameter(Mandatory)]
        [array]$FamilyRules
    )

    $normalized = ConvertTo-TaxonomyPackageId -PackageId $PackageId

    foreach ($rule in @($FamilyRules)) {
        $regex = [string]$rule.Match.PackageIdRegex

        if (
            -not [string]::IsNullOrWhiteSpace($regex) -and
            $normalized -match $regex
        ) {
            $module = ($normalized -split '\.')[-1]

            return [PSCustomObject]@{
                PackageId  = $PackageId
                DisplayName = $null
                Family     = [string]$rule.Family
                Series     = [string]$rule.Series
                Module     = $module
                Category   = [string]$rule.Category
                Roles      = @($rule.Roles)
                Tags       = @($rule.Tags)
                Priority   = $rule.Priority
                Confidence = [string]$rule.Confidence
                Reason     = "Classified by family rule '$($rule.Id)'."
                RuleId     = [string]$rule.Id
                Source     = "FamilyRule"
            }
        }
    }

    return $null
}

function Test-ModTaxonomyDatabase {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Database,

        [Parameter(Mandatory)]
        [array]$InstalledMods
    )

    $errors = @()
    $warnings = @()
    $seenPackageIds = @{}
    $installedLookup = @{}

    foreach ($mod in @($InstalledMods)) {
        if (
            $null -eq $mod -or
            -not ($mod.PSObject.Properties.Name -contains "PackageId")
        ) {
            continue
        }

        $id = ConvertTo-TaxonomyPackageId -PackageId ([string]$mod.PackageId)

        if ($null -ne $id) {
            $installedLookup[$id] = $true
        }
    }

    foreach ($officialId in @(
        "ludeon.rimworld",
        "ludeon.rimworld.royalty",
        "ludeon.rimworld.ideology",
        "ludeon.rimworld.biotech",
        "ludeon.rimworld.anomaly",
        "ludeon.rimworld.odyssey"
    )) {
        $installedLookup[$officialId] = $true
    }

    foreach ($entry in @($Database.Entries)) {
        $id = ConvertTo-TaxonomyPackageId -PackageId ([string]$entry.PackageId)

        if ($null -eq $id) {
            $errors += [PSCustomObject]@{
                Type = "MissingPackageId"
                Message = "Taxonomy entry has no valid PackageId."
            }
            continue
        }

        if ($seenPackageIds.ContainsKey($id)) {
            $errors += [PSCustomObject]@{
                Type = "DuplicatePackageId"
                PackageId = $entry.PackageId
                Message = "Duplicate explicit taxonomy package ID."
            }
        }
        else {
            $seenPackageIds[$id] = $true
        }

        if ($Database.AllowedCategories -notcontains $entry.Category) {
            $errors += [PSCustomObject]@{
                Type = "InvalidCategory"
                PackageId = $entry.PackageId
                Value = $entry.Category
                Message = "Category is not allowed."
            }
        }

        foreach ($role in @($entry.Roles)) {
            if ($Database.AllowedRoles -notcontains $role) {
                $errors += [PSCustomObject]@{
                    Type = "InvalidRole"
                    PackageId = $entry.PackageId
                    Value = $role
                    Message = "Role is not allowed."
                }
            }
        }

        if (-not $installedLookup.ContainsKey($id)) {
            $warnings += [PSCustomObject]@{
                Type = "OptionalEntryNotInstalled"
                PackageId = $entry.PackageId
                Message = "Valid taxonomy entry is not installed."
            }
        }
    }

    $seenRuleIds = @{}

    foreach ($rule in @($Database.FamilyRules)) {
        if ($seenRuleIds.ContainsKey([string]$rule.Id)) {
            $errors += [PSCustomObject]@{
                Type = "DuplicateFamilyRuleId"
                RuleId = $rule.Id
                Message = "Duplicate family rule ID."
            }
        }
        else {
            $seenRuleIds[[string]$rule.Id] = $true
        }

        try {
            [void][regex]::new([string]$rule.Match.PackageIdRegex)
        }
        catch {
            $errors += [PSCustomObject]@{
                Type = "InvalidFamilyRegex"
                RuleId = $rule.Id
                Message = $_.Exception.Message
            }
        }

        if ($Database.AllowedCategories -notcontains $rule.Category) {
            $errors += [PSCustomObject]@{
                Type = "InvalidFamilyCategory"
                RuleId = $rule.Id
                Value = $rule.Category
                Message = "Family rule category is not allowed."
            }
        }

        foreach ($role in @($rule.Roles)) {
            if ($Database.AllowedRoles -notcontains $role) {
                $errors += [PSCustomObject]@{
                    Type = "InvalidFamilyRole"
                    RuleId = $rule.Id
                    Value = $role
                    Message = "Family rule role is not allowed."
                }
            }
        }
    }

    return [PSCustomObject]@{
        ExplicitEntryCount = @($Database.Entries).Count
        FamilyRuleCount    = @($Database.FamilyRules).Count
        ErrorCount         = @($errors).Count
        WarningCount       = @($warnings).Count
        Errors             = @($errors)
        Warnings           = @($warnings)
        IsValid            = (@($errors).Count -eq 0)
    }
}

function Get-ProfileTaxonomySummary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Profile,

        [Parameter(Mandatory)]
        $Database
    )

    $explicitLookup = @{}

    foreach ($entry in @($Database.Entries)) {
        $id = ConvertTo-TaxonomyPackageId -PackageId ([string]$entry.PackageId)

        if ($null -ne $id) {
            $explicitLookup[$id] = $entry
        }
    }

    $classified = @()
    $unclassified = @()
    $familyCounts = @{}
    $seriesCounts = @{}
    $categoryCounts = @{}
    $roleCounts = @{}
    $explicitCount = 0
    $inferredCount = 0

    foreach ($profileEntry in @($Profile.ActiveMods)) {
        $id = [string]$profileEntry.NormalizedPackageId
        $entry = $null
        $source = $null

        if ($explicitLookup.ContainsKey($id)) {
            $entry = $explicitLookup[$id]
            $source = "Explicit"
            $explicitCount++
        }
        else {
            $entry = Get-InferredTaxonomyEntry `
                -PackageId $profileEntry.PackageId `
                -FamilyRules @($Database.FamilyRules)

            if ($null -ne $entry) {
                $source = "FamilyRule"
                $inferredCount++
            }
        }

        if ($null -eq $entry) {
            $unclassified += [PSCustomObject]@{
                Position = $profileEntry.Position
                PackageId = $profileEntry.PackageId
            }
            continue
        }

        $family = if (
            $entry.PSObject.Properties.Name -contains "Family" -and
            -not [string]::IsNullOrWhiteSpace([string]$entry.Family)
        ) {
            [string]$entry.Family
        }
        else {
            "Unspecified"
        }

        $series = if (
            $entry.PSObject.Properties.Name -contains "Series" -and
            -not [string]::IsNullOrWhiteSpace([string]$entry.Series)
        ) {
            [string]$entry.Series
        }
        else {
            $family
        }

        $moduleName = if (
            $entry.PSObject.Properties.Name -contains "Module" -and
            -not [string]::IsNullOrWhiteSpace([string]$entry.Module)
        ) {
            [string]$entry.Module
        }
        else {
            ($profileEntry.PackageId -split '\.')[-1]
        }

        foreach ($tableEntry in @(
            @{ Table = $familyCounts; Key = $family },
            @{ Table = $seriesCounts; Key = $series },
            @{ Table = $categoryCounts; Key = [string]$entry.Category }
        )) {
            if (-not $tableEntry.Table.ContainsKey($tableEntry.Key)) {
                $tableEntry.Table[$tableEntry.Key] = 0
            }
            $tableEntry.Table[$tableEntry.Key]++
        }

        foreach ($role in @($entry.Roles)) {
            if (-not $roleCounts.ContainsKey($role)) {
                $roleCounts[$role] = 0
            }
            $roleCounts[$role]++
        }

        $classified += [PSCustomObject]@{
            Position   = $profileEntry.Position
            PackageId  = $profileEntry.PackageId
            Family     = $family
            Series     = $series
            Module     = $moduleName
            Category   = $entry.Category
            Roles      = @($entry.Roles)
            Tags       = @($entry.Tags)
            Priority   = $entry.Priority
            Confidence = $entry.Confidence
            Source     = $source
            Reason     = $entry.Reason
        }
    }

    return [PSCustomObject]@{
        ProfileName       = $Profile.Name
        ActiveModCount    = $Profile.Count
        ClassifiedCount   = @($classified).Count
        ExplicitCount     = $explicitCount
        InferredCount     = $inferredCount
        UnclassifiedCount = @($unclassified).Count
        CoveragePercent   = if ($Profile.Count -gt 0) {
            [Math]::Round((@($classified).Count / $Profile.Count) * 100, 2)
        }
        else {
            0
        }
        FamilyCounts      = $familyCounts
        SeriesCounts      = $seriesCounts
        CategoryCounts    = $categoryCounts
        RoleCounts        = $roleCounts
        ClassifiedMods    = @($classified)
        UnclassifiedMods  = @($unclassified)
    }
}

function Write-TaxonomyReports {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Validation,

        [Parameter(Mandatory)]
        [array]$ProfileSummaries,

        [Parameter(Mandatory)]
        [string]$OutputFolder
    )

    if (-not (Test-Path -LiteralPath $OutputFolder)) {
        New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
    }

    $reportPath = Join-Path $OutputFolder "TaxonomyReport.json"
    $unclassifiedPath = Join-Path $OutputFolder "UnclassifiedMods.txt"
    $familiesPath = Join-Path $OutputFolder "ModFamilies.json"

    [PSCustomObject]@{
        Generated = (Get-Date).ToString("o")
        Validation = $Validation
        ProfileSummaries = @($ProfileSummaries)
    } |
        ConvertTo-Json -Depth 16 |
        Set-Content -LiteralPath $reportPath -Encoding UTF8

    @(
        foreach ($summary in @($ProfileSummaries)) {
            foreach ($item in @($summary.UnclassifiedMods)) {
                "{0}`t{1}" -f $summary.ProfileName, $item.PackageId
            }
        }
    ) |
        Sort-Object -Unique |
        Set-Content -LiteralPath $unclassifiedPath -Encoding UTF8

    @(
        foreach ($summary in @($ProfileSummaries)) {
            [PSCustomObject]@{
                ProfileName = $summary.ProfileName
                FamilyCounts = $summary.FamilyCounts
                SeriesCounts = $summary.SeriesCounts
            }
        }
    ) |
        ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath $familiesPath -Encoding UTF8

    return [PSCustomObject]@{
        ReportPath = $reportPath
        UnclassifiedPath = $unclassifiedPath
        FamiliesPath = $familiesPath
    }
}

Export-ModuleMember -Function `
    Import-ModTaxonomyDatabase,
    Test-ModTaxonomyDatabase,
    Get-ProfileTaxonomySummary,
    Write-TaxonomyReports
