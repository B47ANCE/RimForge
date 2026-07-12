Set-StrictMode -Version Latest

function Write-AuditReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Mods,

        [Parameter(Mandatory)]
        $Validation,

        [Parameter(Mandatory)]
        $DependencyGraph,

        [Parameter(Mandatory)]
        [string]$OutputFolder,

        [Parameter(Mandatory)]
        [string]$Version
    )

    if (-not (Test-Path $OutputFolder)) {
        New-Item -ItemType Directory -Path $OutputFolder | Out-Null
    }

    # Avoid exporting graph nodes with embedded Mod references,
    # because that would duplicate the full mod records and risk
    # circular/oversized JSON.
    $dependencySummary = [PSCustomObject]@{
        NodeCount              = $DependencyGraph.NodeCount
        DeclaredEdgeCount      = $DependencyGraph.DeclaredEdgeCount
        ResolvedEdgeCount      = $DependencyGraph.ResolvedEdgeCount
        MissingDependencyCount = $DependencyGraph.MissingDependencyCount
        MissingDependencies    = @($DependencyGraph.MissingDependencies)
        CycleCount             = $DependencyGraph.CycleCount
        Cycles                 = @($DependencyGraph.Cycles)
        SuggestedLoadOrder     = @($DependencyGraph.SuggestedLoadOrder)
        UnsortablePackageIds   = @($DependencyGraph.UnsortablePackageIds)
    }

    $validationSummary = [PSCustomObject]@{
        MissingNames = @(
            foreach ($mod in @($Validation.MissingNames)) {
                [PSCustomObject]@{
                    FolderName = $mod.FolderName
                    WorkshopID = $mod.WorkshopID
                    RootPath   = $mod.RootPath
                    PackageId  = $mod.PackageId
                }
            }
        )

        MissingPackageIds = @(
            foreach ($mod in @($Validation.MissingPackageIds)) {
                [PSCustomObject]@{
                    Name       = $mod.Name
                    FolderName = $mod.FolderName
                    WorkshopID = $mod.WorkshopID
                    RootPath   = $mod.RootPath
                }
            }
        )

        MissingAbout = @(
            foreach ($mod in @($Validation.MissingAbout)) {
                [PSCustomObject]@{
                    Name       = $mod.Name
                    FolderName = $mod.FolderName
                    WorkshopID = $mod.WorkshopID
                    RootPath   = $mod.RootPath
                }
            }
        )

        InvalidVersions = @(
            foreach ($mod in @($Validation.InvalidVersions)) {
                [PSCustomObject]@{
                    Name       = $mod.Name
                    PackageId  = $mod.PackageId
                    FolderName = $mod.FolderName
                    WorkshopID = $mod.WorkshopID
                }
            }
        )

        DuplicatePackageIds  = $Validation.DuplicatePackageIds
        DuplicateWorkshopIds = $Validation.DuplicateWorkshopIds
    }

    $report = [PSCustomObject]@{
        AuditVersion    = $Version
        Generated       = (Get-Date).ToString("o")
        ModCount        = $Mods.Count
        Validation      = $validationSummary
        DependencyGraph = $dependencySummary
        Mods            = $Mods
    }

    $jsonPath = Join-Path $OutputFolder "Audit.json"

    $report |
        ConvertTo-Json -Depth 12 |
        Set-Content -Path $jsonPath -Encoding UTF8

    Write-Log SUCCESS "Audit written to $jsonPath"

    return $jsonPath
}

Export-ModuleMember -Function Write-AuditReport