param(
    [string]$ReviewCsv = (
        Join-Path `
            (Split-Path -Parent $PSScriptRoot) `
            "Review\ClassificationReview.csv"
    )
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ReviewCsv -PathType Leaf)) {
    throw "Review CSV not found: $ReviewCsv"
}

$requiredColumns = @(
    "PackageId",
    "ModName",
    "SuggestedCategory",
    "Confidence",
    "TopEvidence",
    "Decision",
    "ApprovedCategory",
    "ApprovedFamily",
    "ApprovedSeries",
    "ApprovedRoles",
    "ApprovedTags",
    "ReviewerNotes"
)

$rows = @(
    Import-Csv -LiteralPath $ReviewCsv
)

if (@($rows).Count -eq 0) {
    Write-Host "Review CSV is valid but contains no rows."
    exit 0
}

$actualColumns = @($rows[0].PSObject.Properties.Name)
$missingColumns = @(
    $requiredColumns |
    Where-Object { $actualColumns -notcontains $_ }
)

if (@($missingColumns).Count -gt 0) {
    throw (
        "Review CSV is missing required column(s): {0}" -f
        ($missingColumns -join ", ")
    )
}

$duplicatePackageIds = @(
    $rows |
    Group-Object {
        ([string]$_.PackageId).Trim().ToLowerInvariant()
    } |
    Where-Object {
        -not [string]::IsNullOrWhiteSpace($_.Name) -and
        $_.Count -gt 1
    }
)

if (@($duplicatePackageIds).Count -gt 0) {
    throw (
        "Review CSV contains duplicate PackageId values: {0}" -f
        (
            @($duplicatePackageIds.Name) -join ", "
        )
    )
}

Write-Host (
    "Review CSV validation passed: {0} row(s)." -f
    @($rows).Count
)
