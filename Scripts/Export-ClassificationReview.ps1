param(
    [string]$ReviewJson = (
        Join-Path `
            (Split-Path -Parent $PSScriptRoot) `
            "Output\ClassificationReview.json"
    ),

    [string]$OutputCsv = (
        Join-Path `
            (Split-Path -Parent $PSScriptRoot) `
            "Review\ClassificationReview.csv"
    )
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-SafeValue {
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

function Convert-EvidenceToText {
    param(
        [AllowNull()]
        $TopScores
    )

    $parts = @()

    foreach ($score in @($TopScores)) {
        $category = [string](Get-SafeValue `
            -InputObject $score `
            -PropertyName "DisplayName" `
            -DefaultValue (
                Get-SafeValue `
                    -InputObject $score `
                    -PropertyName "CategoryId" `
                    -DefaultValue "Unknown"
            ))

        $points = [int](Get-SafeValue `
            -InputObject $score `
            -PropertyName "Score" `
            -DefaultValue 0)

        $evidenceParts = @()

        foreach ($item in @(
            Get-SafeValue `
                -InputObject $score `
                -PropertyName "Evidence" `
                -DefaultValue @()
        )) {
            $type = [string](Get-SafeValue `
                -InputObject $item `
                -PropertyName "Type" `
                -DefaultValue "Evidence")

            $pattern = [string](Get-SafeValue `
                -InputObject $item `
                -PropertyName "Pattern" `
                -DefaultValue (
                    Get-SafeValue `
                        -InputObject $item `
                        -PropertyName "DefType" `
                        -DefaultValue ""
                ))

            $matches = @(
                Get-SafeValue `
                    -InputObject $item `
                    -PropertyName "Matches" `
                    -DefaultValue @()
            )

            $matchText = if (@($matches).Count -gt 0) {
                $matches -join ", "
            }
            else {
                ""
            }

            $detail = @(
                $type,
                $pattern,
                $matchText
            ) |
                Where-Object {
                    -not [string]::IsNullOrWhiteSpace([string]$_)
                }

            if (@($detail).Count -gt 0) {
                $evidenceParts += ($detail -join ": ")
            }
        }

        $summary = "{0}={1}" -f $category, $points

        if (@($evidenceParts).Count -gt 0) {
            $summary += " [" + ($evidenceParts -join " | ") + "]"
        }

        $parts += $summary
    }

    return $parts -join " || "
}

if (-not (Test-Path -LiteralPath $ReviewJson -PathType Leaf)) {
    throw "Classification review file not found: $ReviewJson"
}

try {
    $reviewItems = @(
        Get-Content `
            -LiteralPath $ReviewJson `
            -Raw |
        ConvertFrom-Json
    )
}
catch {
    throw "Classification review JSON is invalid: $($_.Exception.Message)"
}

$rows = @(
    foreach ($item in @($reviewItems)) {
        [PSCustomObject][ordered]@{
            PackageId         = [string](Get-SafeValue $item "PackageId" "")
            ModName           = [string](Get-SafeValue $item "Name" "")
            SuggestedCategory = [string](Get-SafeValue $item "Suggested" "Unclassified")
            Confidence        = [int](Get-SafeValue $item "Confidence" 0)
            TopEvidence       = Convert-EvidenceToText `
                -TopScores (Get-SafeValue $item "TopScores" @())
            Decision          = ""
            ApprovedCategory  = ""
            ApprovedFamily    = ""
            ApprovedSeries    = ""
            ApprovedRoles     = ""
            ApprovedTags      = ""
            ReviewerNotes     = ""
        }
    }
)

$parent = Split-Path -Parent $OutputCsv

if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Path $parent -Force |
        Out-Null
}

$rows |
    Sort-Object PackageId |
    Export-Csv `
        -LiteralPath $OutputCsv `
        -NoTypeInformation `
        -Encoding UTF8

Write-Host (
    "Exported {0} classification review row(s) to {1}" -f
    @($rows).Count,
    $OutputCsv
)
