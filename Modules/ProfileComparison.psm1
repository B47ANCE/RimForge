Set-StrictMode -Version Latest

function Compare-ProfileSet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Profiles
    )

    if (@($Profiles).Count -lt 1) {
        throw "Compare-ProfileSet requires at least one profile."
    }

    $lookups = @{}

    foreach ($profile in @($Profiles)) {
        $lookup = @{}

        foreach ($entry in @($profile.ActiveMods)) {
            $lookup[$entry.NormalizedPackageId] = $entry
        }

        $lookups[$profile.Name] = $lookup
    }

    $allIds = @{}

    foreach ($profile in @($Profiles)) {
        foreach ($entry in @($profile.ActiveMods)) {
            $allIds[$entry.NormalizedPackageId] = $entry.PackageId
        }
    }

    $sharedAcrossAll = @(
        foreach ($id in @($allIds.Keys | Sort-Object)) {
            $presentEverywhere = $true

            foreach ($profile in @($Profiles)) {
                if (-not $lookups[$profile.Name].ContainsKey($id)) {
                    $presentEverywhere = $false
                    break
                }
            }

            if ($presentEverywhere) {
                $allIds[$id]
            }
        }
    )

    $uniqueByProfile = @()

    foreach ($profile in @($Profiles)) {
        $unique = @(
            foreach ($entry in @($profile.ActiveMods)) {
                $presentElsewhere = $false

                foreach ($other in @($Profiles)) {
                    if ($other.Name -eq $profile.Name) {
                        continue
                    }

                    if ($lookups[$other.Name].ContainsKey(
                        $entry.NormalizedPackageId
                    )) {
                        $presentElsewhere = $true
                        break
                    }
                }

                if (-not $presentElsewhere) {
                    $entry.PackageId
                }
            }
        )

        $uniqueByProfile += [PSCustomObject]@{
            ProfileName = $profile.Name
            Count       = @($unique).Count
            PackageIds  = @($unique)
        }
    }

    $pairwise = @()

    for ($i = 0; $i -lt @($Profiles).Count; $i++) {
        for ($j = $i + 1; $j -lt @($Profiles).Count; $j++) {
            $left = $Profiles[$i]
            $right = $Profiles[$j]
            $leftLookup = $lookups[$left.Name]
            $rightLookup = $lookups[$right.Name]

            $shared = @(
                foreach ($entry in @($left.ActiveMods)) {
                    if ($rightLookup.ContainsKey(
                        $entry.NormalizedPackageId
                    )) {
                        $entry.PackageId
                    }
                }
            )

            $leftOnly = @(
                foreach ($entry in @($left.ActiveMods)) {
                    if (-not $rightLookup.ContainsKey(
                        $entry.NormalizedPackageId
                    )) {
                        $entry.PackageId
                    }
                }
            )

            $rightOnly = @(
                foreach ($entry in @($right.ActiveMods)) {
                    if (-not $leftLookup.ContainsKey(
                        $entry.NormalizedPackageId
                    )) {
                        $entry.PackageId
                    }
                }
            )

            $pairwise += [PSCustomObject]@{
                LeftProfile   = $left.Name
                RightProfile  = $right.Name
                SharedCount   = @($shared).Count
                LeftOnlyCount = @($leftOnly).Count
                RightOnlyCount = @($rightOnly).Count
                Shared        = @($shared)
                LeftOnly      = @($leftOnly)
                RightOnly     = @($rightOnly)
            }
        }
    }

    return [PSCustomObject]@{
        ProfileCount          = @($Profiles).Count
        ProfileNames          = @($Profiles.Name)
        SharedAcrossAllCount  = @($sharedAcrossAll).Count
        SharedAcrossAll       = @($sharedAcrossAll)
        UniqueByProfile       = @($uniqueByProfile)
        PairwiseComparisons   = @($pairwise)
    }
}

function Write-ProfileSetReports {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Comparison,

        [Parameter(Mandatory)]
        [array]$ProfileResults,

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

    $jsonPath = Join-Path $OutputFolder "ProfileSetComparison.json"
    $sharedPath = Join-Path $OutputFolder "SharedAcrossAll.txt"

    [PSCustomObject]@{
        Generated      = (Get-Date).ToString("o")
        Comparison     = $Comparison
        ProfileResults = @($ProfileResults)
    } |
        ConvertTo-Json -Depth 14 |
        Set-Content `
            -LiteralPath $jsonPath `
            -Encoding UTF8

    @($Comparison.SharedAcrossAll) |
        Set-Content `
            -LiteralPath $sharedPath `
            -Encoding UTF8

    foreach ($unique in @($Comparison.UniqueByProfile)) {
        $safeName = $unique.ProfileName -replace '[\\/:*?"<>|]', "_"
        $path = Join-Path $OutputFolder ("{0}.unique.txt" -f $safeName)

        @($unique.PackageIds) |
            Set-Content `
                -LiteralPath $path `
                -Encoding UTF8
    }

    foreach ($pair in @($Comparison.PairwiseComparisons)) {
        $left = $pair.LeftProfile -replace '[\\/:*?"<>|]', "_"
        $right = $pair.RightProfile -replace '[\\/:*?"<>|]', "_"

        $pairPath = Join-Path `
            $OutputFolder `
            ("{0}_vs_{1}.json" -f $left, $right)

        $pair |
            ConvertTo-Json -Depth 10 |
            Set-Content `
                -LiteralPath $pairPath `
                -Encoding UTF8
    }

    return $jsonPath
}

Export-ModuleMember -Function `
    Compare-ProfileSet,
    Write-ProfileSetReports
