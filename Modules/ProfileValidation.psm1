Set-StrictMode -Version Latest

function ConvertTo-ValidationPackageId {
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

function New-ValidationModLookup {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Mods
    )

    $lookup = @{}

    foreach ($mod in @($Mods)) {
        if (
            $null -eq $mod -or
            -not ($mod.PSObject.Properties.Name -contains "PackageId")
        ) {
            continue
        }

        $id = ConvertTo-ValidationPackageId `
            -PackageId ([string]$mod.PackageId)

        if ($null -ne $id) {
            $lookup[$id] = $mod
        }
    }

    return $lookup
}

function Test-ModsConfigProfile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Profile,

        [Parameter(Mandatory)]
        [array]$Mods
    )

    $installedLookup = New-ValidationModLookup -Mods $Mods
    $activeLookup = @{}
    $positionLookup = @{}

    foreach ($entry in @($Profile.ActiveMods)) {
        $activeLookup[$entry.NormalizedPackageId] = $true
        $positionLookup[$entry.NormalizedPackageId] = $entry.Position
    }

    $officialIds = @(
        "ludeon.rimworld",
        "ludeon.rimworld.royalty",
        "ludeon.rimworld.ideology",
        "ludeon.rimworld.biotech",
        "ludeon.rimworld.anomaly",
        "ludeon.rimworld.odyssey"
    )

    $missingInstalledMods = @()
    $missingDependencies = @()
    $missingDlc = @()
    $incompatibilities = @()
    $loadOrderIssues = @()

    foreach ($entry in @($Profile.ActiveMods)) {
        $id = $entry.NormalizedPackageId

        if (-not $installedLookup.ContainsKey($id)) {
            if ($officialIds -notcontains $id) {
                $missingInstalledMods += [PSCustomObject]@{
                    Profile   = $Profile.Name
                    PackageId = $entry.PackageId
                    Position  = $entry.Position
                }
            }

            continue
        }

        $mod = $installedLookup[$id]

        foreach ($dependencyRaw in @($mod.Dependencies)) {
            $dependencyId = ConvertTo-ValidationPackageId `
                -PackageId ([string]$dependencyRaw)

            if ($null -eq $dependencyId) {
                continue
            }

            if ($officialIds -contains $dependencyId) {
                if (
                    $dependencyId -ne "ludeon.rimworld" -and
                    -not $activeLookup.ContainsKey($dependencyId)
                ) {
                    $missingDlc += [PSCustomObject]@{
                        Profile             = $Profile.Name
                        RequiredByName      = $mod.Name
                        RequiredByPackageId = $mod.PackageId
                        MissingDlcPackageId = [string]$dependencyRaw
                    }
                }

                continue
            }

            if (-not $activeLookup.ContainsKey($dependencyId)) {
                $missingDependencies += [PSCustomObject]@{
                    Profile             = $Profile.Name
                    RequiredByName      = $mod.Name
                    RequiredByPackageId = $mod.PackageId
                    MissingPackageId    = [string]$dependencyRaw
                }
            }
        }

        foreach ($incompatibleRaw in @($mod.IncompatibleWith)) {
            $incompatibleId = ConvertTo-ValidationPackageId `
                -PackageId ([string]$incompatibleRaw)

            if (
                $null -ne $incompatibleId -and
                $activeLookup.ContainsKey($incompatibleId)
            ) {
                $incompatibilities += [PSCustomObject]@{
                    Profile             = $Profile.Name
                    DeclaredByName      = $mod.Name
                    DeclaredByPackageId = $mod.PackageId
                    IncompatibleWith    = [string]$incompatibleRaw
                }
            }
        }

        foreach ($dependencyRaw in @($mod.Dependencies)) {
            $dependencyId = ConvertTo-ValidationPackageId `
                -PackageId ([string]$dependencyRaw)

            if (
                $null -ne $dependencyId -and
                $positionLookup.ContainsKey($dependencyId) -and
                $positionLookup[$dependencyId] -gt $entry.Position
            ) {
                $loadOrderIssues += [PSCustomObject]@{
                    Profile          = $Profile.Name
                    RuleType         = "Dependency"
                    EarlierPackageId = $entry.PackageId
                    LaterPackageId   = [string]$dependencyRaw
                    Message          = (
                        "{0} loads before required dependency {1}" -f
                        $entry.PackageId,
                        [string]$dependencyRaw
                    )
                }
            }
        }

        foreach ($loadAfterRaw in @($mod.LoadAfter)) {
            $targetId = ConvertTo-ValidationPackageId `
                -PackageId ([string]$loadAfterRaw)

            if (
                $null -ne $targetId -and
                $positionLookup.ContainsKey($targetId) -and
                $positionLookup[$targetId] -gt $entry.Position
            ) {
                $loadOrderIssues += [PSCustomObject]@{
                    Profile          = $Profile.Name
                    RuleType         = "LoadAfter"
                    EarlierPackageId = $entry.PackageId
                    LaterPackageId   = [string]$loadAfterRaw
                    Message          = (
                        "{0} should load after {1}" -f
                        $entry.PackageId,
                        [string]$loadAfterRaw
                    )
                }
            }
        }

        foreach ($loadBeforeRaw in @($mod.LoadBefore)) {
            $targetId = ConvertTo-ValidationPackageId `
                -PackageId ([string]$loadBeforeRaw)

            if (
                $null -ne $targetId -and
                $positionLookup.ContainsKey($targetId) -and
                $positionLookup[$targetId] -lt $entry.Position
            ) {
                $loadOrderIssues += [PSCustomObject]@{
                    Profile          = $Profile.Name
                    RuleType         = "LoadBefore"
                    EarlierPackageId = [string]$loadBeforeRaw
                    LaterPackageId   = $entry.PackageId
                    Message          = (
                        "{0} should load before {1}" -f
                        $entry.PackageId,
                        [string]$loadBeforeRaw
                    )
                }
            }
        }

        foreach ($forceAfterRaw in @($mod.ForceLoadAfter)) {
            $targetId = ConvertTo-ValidationPackageId `
                -PackageId ([string]$forceAfterRaw)

            if (
                $null -ne $targetId -and
                $positionLookup.ContainsKey($targetId) -and
                $positionLookup[$targetId] -gt $entry.Position
            ) {
                $loadOrderIssues += [PSCustomObject]@{
                    Profile          = $Profile.Name
                    RuleType         = "ForceLoadAfter"
                    EarlierPackageId = $entry.PackageId
                    LaterPackageId   = [string]$forceAfterRaw
                    Message          = (
                        "{0} must load after {1}" -f
                        $entry.PackageId,
                        [string]$forceAfterRaw
                    )
                }
            }
        }

        foreach ($forceBeforeRaw in @($mod.ForceLoadBefore)) {
            $targetId = ConvertTo-ValidationPackageId `
                -PackageId ([string]$forceBeforeRaw)

            if (
                $null -ne $targetId -and
                $positionLookup.ContainsKey($targetId) -and
                $positionLookup[$targetId] -lt $entry.Position
            ) {
                $loadOrderIssues += [PSCustomObject]@{
                    Profile          = $Profile.Name
                    RuleType         = "ForceLoadBefore"
                    EarlierPackageId = [string]$forceBeforeRaw
                    LaterPackageId   = $entry.PackageId
                    Message          = (
                        "{0} must load before {1}" -f
                        $entry.PackageId,
                        [string]$forceBeforeRaw
                    )
                }
            }
        }
    }

    return [PSCustomObject]@{
        Profile                   = $Profile.Name
        ActiveModCount            = $Profile.Count
        DuplicateEntries          = @($Profile.DuplicateEntries)
        MissingInstalledMods      = @($missingInstalledMods)
        MissingDependencies       = @($missingDependencies)
        MissingDlc                = @($missingDlc)
        DeclaredIncompatibilities = @($incompatibilities)
        LoadOrderIssues           = @($loadOrderIssues)
    }
}

function Write-ProfileValidationReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $ProfileResult,

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

    $safeName = $ProfileResult.Profile.Name -replace '[\\/:*?"<>|]', "_"
    $path = Join-Path $OutputFolder ("{0}.validation.json" -f $safeName)

    $ProfileResult |
        ConvertTo-Json -Depth 12 |
        Set-Content `
            -LiteralPath $path `
            -Encoding UTF8

    return $path
}

Export-ModuleMember -Function `
    Test-ModsConfigProfile,
    Write-ProfileValidationReport
