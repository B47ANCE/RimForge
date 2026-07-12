Set-StrictMode -Version Latest

function Test-ModLibrary {

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Mods,

        [Parameter(Mandatory)]
        $Index
    )

    $results = [ordered]@{
        MissingNames          = @()
        MissingPackageIds     = @()
        MissingAbout          = @()
        InvalidVersions       = @()
        DuplicatePackageIds   = @(
            @($Index.DuplicatePackageIds) | Where-Object { $null -ne $_ }
        )
        DuplicateWorkshopIds  = @(
            @($Index.DuplicateWorkshopIds) | Where-Object { $null -ne $_ }
        )
    }

    foreach ($mod in $Mods) {

        if ([string]::IsNullOrWhiteSpace($mod.Name)) {
            $results.MissingNames += $mod
        }

        if ([string]::IsNullOrWhiteSpace($mod.PackageId)) {
            $results.MissingPackageIds += $mod
        }

        if (!(Test-Path $mod.AboutPath)) {
            $results.MissingAbout += $mod
        }

        if (@($mod.PSObject.Properties.Match("SupportedVersions")).Count -gt 0) {

            if (@($mod.SupportedVersions).Count -eq 0) {

                $results.InvalidVersions += $mod

            }

        }

    }

    Write-Log SUCCESS "Validation complete."

    Write-Log INFO "Missing Names         : $($results.MissingNames.Count)"
    Write-Log INFO "Missing Package IDs   : $($results.MissingPackageIds.Count)"
    Write-Log INFO "Missing About.xml     : $($results.MissingAbout.Count)"
    Write-Log INFO "Unknown Version       : $($results.InvalidVersions.Count)"
    Write-Log INFO "Duplicate Package IDs : $($results.DuplicatePackageIds.Count)"
    Write-Log INFO "Duplicate Workshop IDs: $($results.DuplicateWorkshopIds.Count)"

    return $results

}

Export-ModuleMember -Function Test-ModLibrary