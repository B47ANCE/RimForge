param(
    [string]$ReviewCsv = (
        Join-Path `
            (Split-Path -Parent $PSScriptRoot) `
            "Review\ClassificationReview.csv"
    ),

    [string]$OverridesJson = (
        Join-Path `
            (Split-Path -Parent $PSScriptRoot) `
            "Database.Curated\ClassificationOverrides.json"
    )
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Convert-ToStringArray {
    param(
        [AllowNull()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return @()
    }

    return @(
        $Value -split '[,;|]' |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
    )
}

function Convert-ToNormalizedPackageId {
    param(
        [AllowNull()]
        [string]$PackageId
    )

    if ([string]::IsNullOrWhiteSpace($PackageId)) {
        return $null
    }

    return $PackageId.Trim().ToLowerInvariant()
}

if (-not (Test-Path -LiteralPath $ReviewCsv -PathType Leaf)) {
    throw "Review CSV not found: $ReviewCsv"
}

$rows = @(
    Import-Csv -LiteralPath $ReviewCsv
)

$validDecisions = @(
    "Approve",
    "Override",
    "Reject",
    "Defer"
)

$approved = @()
$validationErrors = @()

foreach ($row in @($rows)) {
    $packageId = Convert-ToNormalizedPackageId `
        -PackageId ([string]$row.PackageId)

    $decision = ([string]$row.Decision).Trim()

    if ([string]::IsNullOrWhiteSpace($decision)) {
        continue
    }

    $normalizedDecision = @(
        $validDecisions |
        Where-Object {
            $_.Equals(
                $decision,
                [System.StringComparison]::OrdinalIgnoreCase
            )
        }
    ) | Select-Object -First 1

    if ($null -eq $normalizedDecision) {
        $validationErrors += (
            "{0}: invalid Decision '{1}'. Allowed: {2}" -f
            $row.PackageId,
            $decision,
            ($validDecisions -join ", ")
        )
        continue
    }

    if ($null -eq $packageId) {
        $validationErrors += "A reviewed row has no PackageId."
        continue
    }

    if ($normalizedDecision -eq "Defer") {
        continue
    }

    if ($normalizedDecision -eq "Reject") {
        $approved += [PSCustomObject][ordered]@{
            PackageId = $packageId
            Enabled   = $true
            Decision  = "RejectAutomatedClassification"
            Category  = $null
            Family    = $null
            Series    = $null
            Roles     = @()
            Tags      = @()
            Confidence = "HumanReviewed"
            Notes     = [string]$row.ReviewerNotes
        }
        continue
    }

    $category = ([string]$row.ApprovedCategory).Trim()

    if ([string]::IsNullOrWhiteSpace($category)) {
        if ($normalizedDecision -eq "Approve") {
            $category = ([string]$row.SuggestedCategory).Trim()
        }
    }

    if ([string]::IsNullOrWhiteSpace($category)) {
        $validationErrors += (
            "{0}: ApprovedCategory is required for Decision '{1}'." -f
            $packageId,
            $normalizedDecision
        )
        continue
    }

    $approved += [PSCustomObject][ordered]@{
        PackageId = $packageId
        Enabled   = $true
        Decision  = "ClassificationOverride"
        Category  = $category
        Family    = if (
            [string]::IsNullOrWhiteSpace([string]$row.ApprovedFamily)
        ) {
            $null
        }
        else {
            ([string]$row.ApprovedFamily).Trim()
        }
        Series    = if (
            [string]::IsNullOrWhiteSpace([string]$row.ApprovedSeries)
        ) {
            $null
        }
        else {
            ([string]$row.ApprovedSeries).Trim()
        }
        Roles     = @(
            Convert-ToStringArray `
                -Value ([string]$row.ApprovedRoles)
        )
        Tags      = @(
            Convert-ToStringArray `
                -Value ([string]$row.ApprovedTags)
        )
        Confidence = "HumanReviewed"
        Notes     = [string]$row.ReviewerNotes
    }
}

if (@($validationErrors).Count -gt 0) {
    foreach ($message in @($validationErrors)) {
        Write-Error $message
    }

    throw (
        "Review import failed with {0} validation error(s)." -f
        @($validationErrors).Count
    )
}

$existingOverrides = @()

if (Test-Path -LiteralPath $OverridesJson -PathType Leaf) {
    try {
        $existingDocument = Get-Content `
            -LiteralPath $OverridesJson `
            -Raw |
            ConvertFrom-Json

        $existingOverrides = @($existingDocument.Overrides)
    }
    catch {
        throw "Existing overrides JSON is invalid: $($_.Exception.Message)"
    }
}

$lookup = @{}

foreach ($item in @($existingOverrides)) {
    $id = Convert-ToNormalizedPackageId `
        -PackageId ([string]$item.PackageId)

    if ($null -ne $id) {
        $lookup[$id] = $item
    }
}

foreach ($item in @($approved)) {
    $lookup[[string]$item.PackageId] = $item
}

$merged = @(
    foreach ($key in @($lookup.Keys | Sort-Object)) {
        $lookup[$key]
    }
)

$document = [PSCustomObject][ordered]@{
    SchemaVersion = 1
    UpdatedAtUtc  = [DateTime]::UtcNow.ToString("o")
    Overrides     = @($merged)
}

$parent = Split-Path -Parent $OverridesJson

if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Path $parent -Force |
        Out-Null
}

$temporaryPath = "{0}.{1}.tmp" -f
    $OverridesJson,
    [guid]::NewGuid().ToString("N")

try {
    $document |
        ConvertTo-Json -Depth 12 |
        Set-Content `
            -LiteralPath $temporaryPath `
            -Encoding UTF8

    Move-Item `
        -LiteralPath $temporaryPath `
        -Destination $OverridesJson `
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

Write-Host (
    "Imported {0} reviewed classification decision(s). " +
    "ClassificationOverrides.json now contains {1} override(s)." -f
    @($approved).Count,
    @($merged).Count
)
