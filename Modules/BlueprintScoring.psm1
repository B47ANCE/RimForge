Set-StrictMode -Version Latest

function Get-BlueprintSafeValues {
    [CmdletBinding()]
    param(
        [AllowNull()]
        $InputObject,

        [Parameter(Mandatory)]
        [string]$PropertyName
    )

    if ($null -eq $InputObject) {
        return @()
    }

    if ($InputObject.PSObject.Properties.Name -contains $PropertyName) {
        return @($InputObject.$PropertyName)
    }

    return @()
}

function Get-BlueprintSafeProperty {
    [CmdletBinding()]
    param(
        [AllowNull()]
        $InputObject,

        [Parameter(Mandatory)]
        [string]$PropertyName
    )

    if ($null -eq $InputObject) {
        return $null
    }

    if ($InputObject.PSObject.Properties.Name -contains $PropertyName) {
        return $InputObject.$PropertyName
    }

    return $null
}

function Import-BlueprintOverrides {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [PSCustomObject]@{
            SchemaVersion = 1
            Overrides     = @()
            SourcePath    = $Path
        }
    }

    try {
        $data = Get-Content -LiteralPath $Path -Raw |
            ConvertFrom-Json
    }
    catch {
        throw "Blueprint overrides JSON is invalid. $($_.Exception.Message)"
    }

    return [PSCustomObject]@{
        SchemaVersion = if ($null -ne $data.SchemaVersion) {
            $data.SchemaVersion
        }
        else {
            1
        }
        Overrides  = @($data.Overrides)
        SourcePath = $Path
    }
}

function Get-BlueprintCriterionScore {
    [CmdletBinding()]
    param(
        [AllowNull()]
        $ScoreMap,

        [Parameter(Mandatory)]
        [string[]]$CandidateValues,

        [Parameter(Mandatory)]
        [string]$CriterionType
    )

    $score = 0
    $matches = @()

    if ($null -eq $ScoreMap) {
        return [PSCustomObject]@{
            Score   = 0
            Matches = @()
        }
    }

    foreach ($property in @($ScoreMap.PSObject.Properties)) {
        $name = [string]$property.Name
        $weight = [int]$property.Value

        if (@($CandidateValues) -contains $name) {
            $score += $weight

            $matches += [PSCustomObject]@{
                Criterion = $CriterionType
                Value     = $name
                Score     = $weight
            }
        }
    }

    return [PSCustomObject]@{
        Score   = $score
        Matches = @($matches)
    }
}

function Get-BlueprintSectionScores {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $TaxonomyEntry,

        [Parameter(Mandatory)]
        $Blueprint
    )

    $categories = @([string]$TaxonomyEntry.Category)
    $roles = @(
        Get-BlueprintSafeValues `
            -InputObject $TaxonomyEntry `
            -PropertyName "Roles"
    )
    $tags = @(
        Get-BlueprintSafeValues `
            -InputObject $TaxonomyEntry `
            -PropertyName "Tags"
    )
    $families = @()

    if ($TaxonomyEntry.PSObject.Properties.Name -contains "Family") {
        $families = @([string]$TaxonomyEntry.Family)
    }

    $seriesValues = @()

    if ($TaxonomyEntry.PSObject.Properties.Name -contains "Series") {
        $seriesValues = @([string]$TaxonomyEntry.Series)
    }

    $results = @()

    foreach ($section in @($Blueprint.Sections)) {
        if ($section.Id -eq "unclassified") {
            continue
        }

        $maps = Get-BlueprintSafeProperty `
            -InputObject $section `
            -PropertyName "MatchScores"

        $categoryMap = Get-BlueprintSafeProperty `
            -InputObject $maps `
            -PropertyName "Categories"

        $roleMap = Get-BlueprintSafeProperty `
            -InputObject $maps `
            -PropertyName "Roles"

        $tagMap = Get-BlueprintSafeProperty `
            -InputObject $maps `
            -PropertyName "Tags"

        $familyMap = Get-BlueprintSafeProperty `
            -InputObject $maps `
            -PropertyName "Families"

        $seriesMap = Get-BlueprintSafeProperty `
            -InputObject $maps `
            -PropertyName "Series"

        $categoryResult = Get-BlueprintCriterionScore `
            -ScoreMap $categoryMap `
            -CandidateValues $categories `
            -CriterionType "Category"

        $roleResult = Get-BlueprintCriterionScore `
            -ScoreMap $roleMap `
            -CandidateValues $roles `
            -CriterionType "Role"

        $tagResult = Get-BlueprintCriterionScore `
            -ScoreMap $tagMap `
            -CandidateValues $tags `
            -CriterionType "Tag"

        $familyResult = Get-BlueprintCriterionScore `
            -ScoreMap $familyMap `
            -CandidateValues $families `
            -CriterionType "Family"

        $seriesResult = Get-BlueprintCriterionScore `
            -ScoreMap $seriesMap `
            -CandidateValues $seriesValues `
            -CriterionType "Series"

        $allMatches = @(
            $categoryResult.Matches
            $roleResult.Matches
            $tagResult.Matches
            $familyResult.Matches
            $seriesResult.Matches
        )

        $total = (
            $categoryResult.Score +
            $roleResult.Score +
            $tagResult.Score +
            $familyResult.Score +
            $seriesResult.Score
        )

        $results += [PSCustomObject]@{
            SectionId    = [string]$section.Id
            SectionName  = [string]$section.Name
            SectionOrder = [int]$section.Order
            Score        = [int]$total
            Matches      = @($allMatches)
        }
    }

    return @($results)
}

function Resolve-BlueprintSection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $TaxonomyEntry,

        [Parameter(Mandatory)]
        $Blueprint,

        [Parameter(Mandatory)]
        $Overrides,

        [Parameter(Mandatory)]
        [int]$OriginalPosition
    )

    $packageId = [string]$TaxonomyEntry.PackageId
    $override = @(
        $Overrides.Overrides |
        Where-Object {
            ([string]$_.PackageId).ToLowerInvariant() -eq
            $packageId.ToLowerInvariant()
        }
    ) | Select-Object -First 1

    if ($null -ne $override) {
        $section = @(
            $Blueprint.Sections |
            Where-Object {
                [string]$_.Id -eq [string]$override.BlueprintSection
            }
        ) | Select-Object -First 1

        if ($null -eq $section) {
            throw (
                "Blueprint override for {0} references unknown section {1}." -f
                $packageId,
                $override.BlueprintSection
            )
        }

        return [PSCustomObject]@{
            PackageId       = $packageId
            SectionId       = [string]$section.Id
            SectionName     = [string]$section.Name
            SectionOrder    = [int]$section.Order
            ResolutionType  = "ExplicitOverride"
            WinningScore    = $null
            OriginalPosition = $OriginalPosition
            Explanation     = [string]$override.Reason
            ScoreBreakdown  = @()
        }
    }

    $scores = Get-BlueprintSectionScores `
        -TaxonomyEntry $TaxonomyEntry `
        -Blueprint $Blueprint

    $best = @(
        $scores |
        Sort-Object `
            @{ Expression = { $_.Score }; Descending = $true },
            @{ Expression = { $_.SectionOrder }; Descending = $false }
    ) | Select-Object -First 1

    if ($null -eq $best -or $best.Score -le 0) {
        $section = @(
            $Blueprint.Sections |
            Where-Object { $_.Id -eq "unclassified" }
        ) | Select-Object -First 1

        return [PSCustomObject]@{
            PackageId       = $packageId
            SectionId       = [string]$section.Id
            SectionName     = [string]$section.Name
            SectionOrder    = [int]$section.Order
            ResolutionType  = "Unclassified"
            WinningScore    = 0
            OriginalPosition = $OriginalPosition
            Explanation     = "No positive blueprint score."
            ScoreBreakdown  = @($scores)
        }
    }

    $winningReasons = @(
        foreach ($match in @($best.Matches)) {
            "{0} '{1}' scored {2}" -f
            $match.Criterion,
            $match.Value,
            $match.Score
        }
    )

    return [PSCustomObject]@{
        PackageId       = $packageId
        SectionId       = [string]$best.SectionId
        SectionName     = [string]$best.SectionName
        SectionOrder    = [int]$best.SectionOrder
        ResolutionType  = "ScoredMatch"
        WinningScore    = [int]$best.Score
        OriginalPosition = $OriginalPosition
        Explanation     = $winningReasons -join "; "
        ScoreBreakdown  = @($scores)
    }
}

Export-ModuleMember -Function `
    Import-BlueprintOverrides,
    Get-BlueprintSectionScores,
    Resolve-BlueprintSection
