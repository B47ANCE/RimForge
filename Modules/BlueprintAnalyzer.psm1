Set-StrictMode -Version Latest

function Import-LoadOrderBlueprint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Load-order blueprint not found: $Path"
    }

    try {
        $data = Get-Content -LiteralPath $Path -Raw |
            ConvertFrom-Json
    }
    catch {
        throw "Load-order blueprint JSON is invalid. $($_.Exception.Message)"
    }

    $sections = @($data.Sections | Sort-Object Order)
    $seenIds = @{}
    $errors = @()

    foreach ($section in @($sections)) {
        if ([string]::IsNullOrWhiteSpace([string]$section.Id)) {
            $errors += "A blueprint section is missing Id."
            continue
        }

        if ($seenIds.ContainsKey([string]$section.Id)) {
            $errors += "Duplicate blueprint section Id: $($section.Id)"
        }
        else {
            $seenIds[[string]$section.Id] = $true
        }
    }

    if (@($errors).Count -gt 0) {
        throw ($errors -join [Environment]::NewLine)
    }

    return [PSCustomObject]@{
        SchemaVersion      = $data.SchemaVersion
        Name               = $data.Name
        Description        = $data.Description
        HardRulePrecedence = [bool]$data.HardRulePrecedence
        TieBreaker         = [string]$data.TieBreaker
        Sections           = @($sections)
        SourcePath         = $Path
    }
}

function Get-MinimalBlueprintMoves {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Assignments
    )

    $ordered = @(
        $Assignments |
        Where-Object { $_.SectionId -ne "unclassified" } |
        Sort-Object OriginalPosition
    )

    if (@($ordered).Count -eq 0) {
        return [PSCustomObject]@{
            MoveCount = 0
            Moves     = @()
        }
    }

    $values = @(
        foreach ($item in @($ordered)) {
            [int]$item.SectionOrder
        }
    )

    $lengths = New-Object int[] @($values).Count
    $previous = New-Object int[] @($values).Count

    for ($i = 0; $i -lt @($values).Count; $i++) {
        $lengths[$i] = 1
        $previous[$i] = -1

        for ($j = 0; $j -lt $i; $j++) {
            if (
                $values[$j] -le $values[$i] -and
                ($lengths[$j] + 1) -gt $lengths[$i]
            ) {
                $lengths[$i] = $lengths[$j] + 1
                $previous[$i] = $j
            }
        }
    }

    $bestIndex = 0

    for ($i = 1; $i -lt @($values).Count; $i++) {
        if ($lengths[$i] -gt $lengths[$bestIndex]) {
            $bestIndex = $i
        }
    }

    $keepIndexes = @()
    $cursor = $bestIndex

    while ($cursor -ge 0) {
        $keepIndexes = @($cursor) + $keepIndexes
        $cursor = $previous[$cursor]
    }

    $keepLookup = @{}

    foreach ($index in @($keepIndexes)) {
        $keepLookup[$index] = $true
    }

    $moves = @()

    for ($i = 0; $i -lt @($ordered).Count; $i++) {
        if ($keepLookup.ContainsKey($i)) {
            continue
        }

        $item = $ordered[$i]

        $moves += [PSCustomObject]@{
            PackageId        = $item.PackageId
            CurrentPosition  = $item.OriginalPosition
            ExpectedSection  = $item.SectionName
            SectionOrder     = $item.SectionOrder
            ResolutionType   = $item.ResolutionType
            Explanation      = $item.Explanation
        }
    }

    return [PSCustomObject]@{
        MoveCount = @($moves).Count
        Moves     = @($moves)
    }
}

function Test-ProfileBlueprint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $ProfileTaxonomySummary,

        [Parameter(Mandatory)]
        $Blueprint,

        [Parameter(Mandatory)]
        $Overrides
    )

    $assignments = @()

    foreach ($entry in @($ProfileTaxonomySummary.ClassifiedMods)) {
        $resolved = Resolve-BlueprintSection `
            -TaxonomyEntry $entry `
            -Blueprint $Blueprint `
            -Overrides $Overrides `
            -OriginalPosition ([int]$entry.Position)

        $assignments += [PSCustomObject]@{
            Position        = [int]$entry.Position
            OriginalPosition = [int]$entry.Position
            PackageId       = [string]$entry.PackageId
            Family          = [string]$entry.Family
            Series          = [string]$entry.Series
            Category        = [string]$entry.Category
            Roles           = @($entry.Roles)
            Tags            = @($entry.Tags)
            SectionId       = $resolved.SectionId
            SectionName     = $resolved.SectionName
            SectionOrder    = $resolved.SectionOrder
            ResolutionType  = $resolved.ResolutionType
            WinningScore    = $resolved.WinningScore
            Explanation     = $resolved.Explanation
            ScoreBreakdown  = @($resolved.ScoreBreakdown)
        }
    }

    foreach ($entry in @($ProfileTaxonomySummary.UnclassifiedMods)) {
        $assignments += [PSCustomObject]@{
            Position        = [int]$entry.Position
            OriginalPosition = [int]$entry.Position
            PackageId       = [string]$entry.PackageId
            Family          = $null
            Series          = $null
            Category        = "Unknown"
            Roles           = @()
            Tags            = @()
            SectionId       = "unclassified"
            SectionName     = "Unclassified"
            SectionOrder    = 999
            ResolutionType  = "Unclassified"
            WinningScore    = 0
            Explanation     = "No taxonomy entry."
            ScoreBreakdown  = @()
        }
    }

    $assignments = @($assignments | Sort-Object OriginalPosition)

    $minimal = Get-MinimalBlueprintMoves `
        -Assignments @($assignments)

    $classifiedCount = @(
        $assignments |
        Where-Object { $_.SectionId -ne "unclassified" }
    ).Count

    $score = if ($classifiedCount -gt 0) {
        [Math]::Round(
            (($classifiedCount - $minimal.MoveCount) /
            $classifiedCount) * 100,
            2
        )
    }
    else {
        0
    }

    $sectionCounts = @{}

    foreach ($assignment in @($assignments)) {
        if (-not $sectionCounts.ContainsKey($assignment.SectionName)) {
            $sectionCounts[$assignment.SectionName] = 0
        }

        $sectionCounts[$assignment.SectionName]++
    }

    $suggestedOrder = @(
        $assignments |
        Sort-Object `
            @{ Expression = { $_.SectionOrder } },
            @{ Expression = { $_.OriginalPosition } } |
        ForEach-Object { $_.PackageId }
    )

    return [PSCustomObject]@{
        ProfileName           = $ProfileTaxonomySummary.ProfileName
        ActiveModCount        = $ProfileTaxonomySummary.ActiveModCount
        ClassifiedCount       = $classifiedCount
        UnclassifiedCount     = @(
            $assignments |
            Where-Object { $_.SectionId -eq "unclassified" }
        ).Count
        BlueprintScore        = $score
        MinimalMoveCount      = $minimal.MoveCount
        SectionCounts         = $sectionCounts
        Assignments           = @($assignments)
        MinimalMoves          = @($minimal.Moves)
        SuggestedSectionOrder = @($suggestedOrder)
    }
}

function Write-BlueprintReports {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Blueprint,

        [Parameter(Mandatory)]
        [array]$ProfileBlueprintResults,

        [Parameter(Mandatory)]
        [string]$OutputFolder
    )

    if (-not (Test-Path -LiteralPath $OutputFolder)) {
        New-Item -ItemType Directory -Path $OutputFolder -Force |
            Out-Null
    }

    $reportPath = Join-Path $OutputFolder "LoadOrderBlueprintReport.json"
    $movesPath = Join-Path $OutputFolder "BlueprintMinimalMoves.txt"

    [PSCustomObject]@{
        Generated = (Get-Date).ToString("o")
        Blueprint = $Blueprint
        Profiles  = @($ProfileBlueprintResults)
    } |
        ConvertTo-Json -Depth 18 |
        Set-Content -LiteralPath $reportPath -Encoding UTF8

    @(
        foreach ($profile in @($ProfileBlueprintResults)) {
            foreach ($move in @($profile.MinimalMoves)) {
                "{0}`t{1}`t{2}`t{3}" -f
                $profile.ProfileName,
                $move.PackageId,
                $move.ExpectedSection,
                $move.Explanation
            }
        }
    ) |
        Set-Content -LiteralPath $movesPath -Encoding UTF8

    foreach ($profile in @($ProfileBlueprintResults)) {
        $safeName = $profile.ProfileName -replace '[\\/:*?"<>|]', "_"
        $orderPath = Join-Path `
            $OutputFolder `
            ("{0}.BlueprintSuggestedOrder.txt" -f $safeName)

        @($profile.SuggestedSectionOrder) |
            Set-Content -LiteralPath $orderPath -Encoding UTF8
    }

    return [PSCustomObject]@{
        ReportPath = $reportPath
        MovesPath  = $movesPath
    }
}

Export-ModuleMember -Function `
    Import-LoadOrderBlueprint,
    Test-ProfileBlueprint,
    Write-BlueprintReports
