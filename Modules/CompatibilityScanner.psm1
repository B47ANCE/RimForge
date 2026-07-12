Set-StrictMode -Version Latest

function ConvertTo-CompatibilityPackageId {
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

function New-CompatibilityModLookup {
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

        $id = ConvertTo-CompatibilityPackageId `
            -PackageId ([string]$mod.PackageId)

        if ($null -ne $id) {
            $lookup[$id] = $mod
        }
    }

    return $lookup
}

function Import-CompatibilityRules {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [PSCustomObject]@{
            SchemaVersion = 1
            Rules         = @()
        }
    }

    try {
        $data = Get-Content `
            -LiteralPath $Path `
            -Raw |
            ConvertFrom-Json
    }
    catch {
        throw (
            "Compatibility rules file is invalid JSON: {0}. {1}" -f
            $Path,
            $_.Exception.Message
        )
    }

    return [PSCustomObject]@{
        SchemaVersion = if ($null -ne $data.SchemaVersion) {
            $data.SchemaVersion
        }
        else {
            1
        }
        Rules = @($data.Rules)
    }
}

function Test-ProfileCompatibility {
    <#
    .SYNOPSIS
        Checks one active profile for declared and curated incompatibilities.

    .DESCRIPTION
        Declared checks come from each mod's About.xml incompatibleWith list.
        Curated checks come from CompatibilityRules.json. Package ID matching
        is case-insensitive and every finding is deduplicated.
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Profile,

        [Parameter(Mandatory)]
        [array]$Mods,

        [Parameter(Mandatory)]
        $Rules
    )

    $modLookup = New-CompatibilityModLookup -Mods $Mods
    $activeLookup = @{}

    foreach ($entry in @($Profile.ActiveMods)) {
        $activeLookup[$entry.NormalizedPackageId] = $entry
    }

    $declaredFindings = @()
    $customFindings = @()
    $findingKeys = @{}

    # -------------------------------------------------------------
    # About.xml incompatibleWith declarations
    # -------------------------------------------------------------

    foreach ($entry in @($Profile.ActiveMods)) {
        $sourceId = [string]$entry.NormalizedPackageId

        if (-not $modLookup.ContainsKey($sourceId)) {
            continue
        }

        $sourceMod = $modLookup[$sourceId]

        foreach ($rawTarget in @($sourceMod.IncompatibleWith)) {
            $targetId = ConvertTo-CompatibilityPackageId `
                -PackageId ([string]$rawTarget)

            if (
                $null -eq $targetId -or
                -not $activeLookup.ContainsKey($targetId)
            ) {
                continue
            }

            $pair = @($sourceId, $targetId) | Sort-Object
            $key = "declared|{0}" -f ($pair -join "|")

            if ($findingKeys.ContainsKey($key)) {
                continue
            }

            $findingKeys[$key] = $true

            $targetMod = if ($modLookup.ContainsKey($targetId)) {
                $modLookup[$targetId]
            }
            else {
                $null
            }

            $declaredFindings += [PSCustomObject]@{
                Source          = "AboutXml"
                Severity        = "Error"
                RuleId          = $null
                Profile         = $Profile.Name
                FirstPackageId  = $sourceMod.PackageId
                FirstName       = $sourceMod.Name
                SecondPackageId = if ($null -ne $targetMod) {
                    $targetMod.PackageId
                }
                else {
                    [string]$rawTarget
                }
                SecondName      = if ($null -ne $targetMod) {
                    $targetMod.Name
                }
                else {
                    $null
                }
                Reason          = (
                    "{0} declares {1} as incompatible in About.xml." -f
                    $sourceMod.PackageId,
                    [string]$rawTarget
                )
                Recommendation  = "Do not activate both mods in the same profile."
            }
        }
    }

    # -------------------------------------------------------------
    # Curated rules database
    # -------------------------------------------------------------

    foreach ($rule in @($Rules.Rules)) {
        if (
            $rule.PSObject.Properties.Name -contains "Enabled" -and
            $rule.Enabled -eq $false
        ) {
            continue
        }

        $packageIds = @(
            foreach ($rawId in @($rule.PackageIds)) {
                $normalized = ConvertTo-CompatibilityPackageId `
                    -PackageId ([string]$rawId)

                if ($null -ne $normalized) {
                    $normalized
                }
            }
        )

        if (@($packageIds).Count -lt 2) {
            continue
        }

        $appliesToProfiles = @(
            foreach ($name in @($rule.AppliesToProfiles)) {
                if (-not [string]::IsNullOrWhiteSpace([string]$name)) {
                    ([string]$name).Trim()
                }
            }
        )

        if (
            @($appliesToProfiles).Count -gt 0 -and
            $appliesToProfiles -notcontains $Profile.Name
        ) {
            continue
        }

        $allActive = $true

        foreach ($id in @($packageIds)) {
            if (-not $activeLookup.ContainsKey($id)) {
                $allActive = $false
                break
            }
        }

        if (-not $allActive) {
            continue
        }

        $ruleId = if (
            $rule.PSObject.Properties.Name -contains "Id" -and
            -not [string]::IsNullOrWhiteSpace([string]$rule.Id)
        ) {
            [string]$rule.Id
        }
        else {
            $packageIds -join "+"
        }

        $key = "custom|{0}|{1}" -f (
            $ruleId,
            (@($packageIds | Sort-Object) -join "|")
        )

        if ($findingKeys.ContainsKey($key)) {
            continue
        }

        $findingKeys[$key] = $true

        $matchedMods = @(
            foreach ($id in @($packageIds)) {
                if ($modLookup.ContainsKey($id)) {
                    [PSCustomObject]@{
                        PackageId = $modLookup[$id].PackageId
                        Name      = $modLookup[$id].Name
                    }
                }
                else {
                    [PSCustomObject]@{
                        PackageId = $activeLookup[$id].PackageId
                        Name      = $null
                    }
                }
            }
        )

        $customFindings += [PSCustomObject]@{
            Source         = "CompatibilityRules"
            Severity       = if (
                -not [string]::IsNullOrWhiteSpace([string]$rule.Severity)
            ) {
                [string]$rule.Severity
            }
            else {
                "Warning"
            }
            RuleId         = $ruleId
            Profile        = $Profile.Name
            Mods           = @($matchedMods)
            Reason         = [string]$rule.Reason
            Recommendation = [string]$rule.Recommendation
            Reference      = [string]$rule.Reference
        }
    }

    $allFindings = @(
        $declaredFindings
        $customFindings
    )

    return [PSCustomObject]@{
        Profile               = $Profile.Name
        DeclaredCount         = @($declaredFindings).Count
        CustomRuleCount       = @($customFindings).Count
        TotalCount            = @($allFindings).Count
        DeclaredFindings      = @($declaredFindings)
        CustomRuleFindings    = @($customFindings)
        Findings              = @($allFindings)
    }
}

function Write-CompatibilityReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $CompatibilityResult,

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

    $safeName = $CompatibilityResult.Profile -replace '[\\/:*?"<>|]', "_"
    $path = Join-Path `
        $OutputFolder `
        ("{0}.compatibility.json" -f $safeName)

    $CompatibilityResult |
        ConvertTo-Json -Depth 12 |
        Set-Content `
            -LiteralPath $path `
            -Encoding UTF8

    return $path
}

Export-ModuleMember -Function `
    Import-CompatibilityRules,
    Test-ProfileCompatibility,
    Write-CompatibilityReport
