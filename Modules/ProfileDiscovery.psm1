Set-StrictMode -Version Latest

function ConvertTo-ProfilePackageId {
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

function Import-ModsConfigProfile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Name
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Profile file not found: $Path"
    }

    $raw = Get-Content -LiteralPath $Path -Raw
    $trimmed = $raw.TrimStart()

    $activeMods = @()
    $knownExpansions = @()
    $version = $null
    $format = $null

    if ($trimmed.StartsWith("{")) {
        $format = "JSON"
        $data = $raw | ConvertFrom-Json

        $version = [string]$data.version
        $activeMods = @($data.activeMods)
        $knownExpansions = @($data.knownExpansions)
    }
    elseif ($trimmed.StartsWith("<")) {
        $format = "XML"

        $xml = New-Object System.Xml.XmlDocument
        $xml.PreserveWhitespace = $false
        $xml.Load($Path)

        $versionNode = $xml.SelectSingleNode("/ModsConfigData/version")

        if ($null -ne $versionNode) {
            $version = ([string]$versionNode.InnerText).Trim()
        }

        $activeMods = @(
            foreach ($node in @(
                $xml.SelectNodes("/ModsConfigData/activeMods/li")
            )) {
                $value = ([string]$node.InnerText).Trim()

                if (-not [string]::IsNullOrWhiteSpace($value)) {
                    $value
                }
            }
        )

        $knownExpansions = @(
            foreach ($node in @(
                $xml.SelectNodes("/ModsConfigData/knownExpansions/li")
            )) {
                $value = ([string]$node.InnerText).Trim()

                if (-not [string]::IsNullOrWhiteSpace($value)) {
                    $value
                }
            }
        )
    }
    else {
        throw "Unsupported profile format in: $Path"
    }

    $ordered = @()
    $seen = @{}
    $duplicates = @()

    for ($index = 0; $index -lt @($activeMods).Count; $index++) {
        $rawPackageId = [string]$activeMods[$index]
        $normalized = ConvertTo-ProfilePackageId `
            -PackageId $rawPackageId

        if ($null -eq $normalized) {
            continue
        }

        if ($seen.ContainsKey($normalized)) {
            $duplicates += [PSCustomObject]@{
                PackageId      = $rawPackageId
                FirstPosition  = $seen[$normalized]
                RepeatPosition = $index
            }

            continue
        }

        $seen[$normalized] = $index

        $ordered += [PSCustomObject]@{
            Position            = $index
            PackageId           = $rawPackageId
            NormalizedPackageId = $normalized
        }
    }

    return [PSCustomObject]@{
        Name             = $Name
        Path             = $Path
        Format           = $format
        Version          = $version
        ActiveMods       = @($ordered)
        PackageIds       = @(
            foreach ($entry in @($ordered)) {
                $entry.PackageId
            }
        )
        NormalizedIds    = @(
            foreach ($entry in @($ordered)) {
                $entry.NormalizedPackageId
            }
        )
        KnownExpansions  = @($knownExpansions)
        DuplicateEntries = @($duplicates)
        Count            = @($ordered).Count
    }
}

function Find-ModsConfigProfiles {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ProfilesFolder
    )

    if (-not (Test-Path -LiteralPath $ProfilesFolder -PathType Container)) {
        return @()
    }

    $profiles = @()

    foreach ($file in @(
        Get-ChildItem `
            -LiteralPath $ProfilesFolder `
            -File `
            -Filter "*.xml" |
        Sort-Object Name
    )) {
        try {
            $profile = Import-ModsConfigProfile `
                -Path $file.FullName `
                -Name $file.BaseName

            $hasCore = @($profile.NormalizedIds) -contains "ludeon.rimworld"

            if ($profile.Count -gt 0 -and $hasCore) {
                $profiles += $profile

                Write-Log SUCCESS (
                    "Loaded profile '{0}' from {1} ({2} mods)" -f
                    $profile.Name,
                    $file.Name,
                    $profile.Count
                )
            }
            else {
                Write-Log DEBUG (
                    "Ignored XML file that is not a populated ModsConfig: {0}" -f
                    $file.Name
                )
            }
        }
        catch {
            Write-Log DEBUG (
                "Ignored non-ModsConfig XML file '{0}': {1}" -f
                $file.Name,
                $_.Exception.Message
            )
        }
    }

    return @($profiles)
}

Export-ModuleMember -Function `
    ConvertTo-ProfilePackageId,
    Import-ModsConfigProfile,
    Find-ModsConfigProfiles
