Set-StrictMode -Version Latest

function New-ModIndex {

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Mods
    )

    $index = [ordered]@{
        ByPackageId = @{}
        ByWorkshopId = @{}
        ByName = @{}

        DuplicatePackageIds = @{}
        DuplicateWorkshopIds = @{}
    }

    foreach ($mod in $Mods) {

        #
        # PackageId
        #

        if (-not [string]::IsNullOrWhiteSpace($mod.PackageId)) {

            if ($index.ByPackageId.ContainsKey($mod.PackageId)) {

                if (-not $index.DuplicatePackageIds.ContainsKey($mod.PackageId)) {

                    $index.DuplicatePackageIds[$mod.PackageId] = @(
                        $index.ByPackageId[$mod.PackageId]
                    )

                }

                $index.DuplicatePackageIds[$mod.PackageId] += $mod

            }
            else {

                $index.ByPackageId[$mod.PackageId] = $mod

            }

        }

        #
        # Workshop ID
        #

        if (-not [string]::IsNullOrWhiteSpace($mod.WorkshopID)) {

            if ($index.ByWorkshopId.ContainsKey($mod.WorkshopID)) {

                if (-not $index.DuplicateWorkshopIds.ContainsKey($mod.WorkshopID)) {

                    $index.DuplicateWorkshopIds[$mod.WorkshopID] = @(
                        $index.ByWorkshopId[$mod.WorkshopID]
                    )

                }

                $index.DuplicateWorkshopIds[$mod.WorkshopID] += $mod

            }
            else {

                $index.ByWorkshopId[$mod.WorkshopID] = $mod

            }

        }

        #
        # Name
        #

        if (-not [string]::IsNullOrWhiteSpace($mod.Name)) {

            $index.ByName[$mod.Name] = $mod

        }

    }

    Write-Log SUCCESS "Built indexes."

    Write-Log INFO "Package IDs : $($index.ByPackageId.Count)"

    Write-Log INFO "Workshop IDs: $($index.ByWorkshopId.Count)"

    Write-Log INFO "Names       : $($index.ByName.Count)"

    Write-Log INFO "Duplicate Package IDs : $($index.DuplicatePackageIds.Count)"

    Write-Log INFO "Duplicate Workshop IDs: $($index.DuplicateWorkshopIds.Count)"

    return $index

}

Export-ModuleMember -Function New-ModIndex