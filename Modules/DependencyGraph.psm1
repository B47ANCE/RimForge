Set-StrictMode -Version Latest

function New-DependencyGraph {
    <#
    .SYNOPSIS
        Builds a dependency graph from parsed RimWorld mod metadata.

    .DESCRIPTION
        Resolves each declared dependency against the PackageId index,
        builds forward and reverse edges, detects missing dependencies,
        detects cycles, and calculates a dependency-safe ordering.

    .PARAMETER Mods
        Parsed RimWorld mod records.

    .PARAMETER Index
        Index created by New-ModIndex.
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Mods,

        [Parameter(Mandatory)]
        $Index
    )

    $nodes = @{}
    $missingDependencies = @()
    $declaredEdgeCount = 0
    $resolvedEdgeCount = 0

    # ------------------------------------------------------------
    # Create one graph node for every mod with a PackageId
    # ------------------------------------------------------------

    foreach ($mod in $Mods) {
        if ([string]::IsNullOrWhiteSpace([string]$mod.PackageId)) {
            continue
        }

        $packageId = [string]$mod.PackageId

        $officialPackages = @("ludeon.rimworld",
            "ludeon.rimworld.royalty",
            "ludeon.rimworld.ideology",
            "ludeon.rimworld.biotech",
            "ludeon.rimworld.anomaly",
            "ludeon.rimworld.odyssey")

        $nodes[$packageId] = [PSCustomObject]@{
            PackageId    = $packageId
            Name         = $mod.Name
            Mod          = $mod
            Dependencies = @()
            RequiredBy   = @()
        }

        # Add non-circular, JSON-safe graph information to ModRecord.
        $mod | Add-Member `
            -NotePropertyName ResolvedDependencies `
            -NotePropertyValue @() `
            -Force

        $mod | Add-Member `
            -NotePropertyName RequiredBy `
            -NotePropertyValue @() `
            -Force

        $mod | Add-Member `
            -NotePropertyName MissingDependencies `
            -NotePropertyValue @() `
            -Force
    }

    # ------------------------------------------------------------
    # Resolve dependency edges
    # ------------------------------------------------------------

    foreach ($mod in $Mods) {
        if ([string]::IsNullOrWhiteSpace([string]$mod.PackageId)) {
            continue
        }

        $sourceId = [string]$mod.PackageId

        if (-not $nodes.ContainsKey($sourceId)) {
            continue
        }

        $dependencies = @($mod.Dependencies)

        foreach ($rawDependencyId in $dependencies) {
            $dependencyId = ([string]$rawDependencyId).Trim()

            if ([string]::IsNullOrWhiteSpace($dependencyId)) {
                continue
            }

            $declaredEdgeCount++

            # Prevent duplicate declarations from creating duplicate edges.
            if ($nodes[$sourceId].Dependencies -contains $dependencyId) {
                continue
            }

            $normalizedDependencyId = $dependencyId.ToLowerInvariant()

            if ($officialPackages -contains $normalizedDependencyId) {
                $mod.ResolvedDependencies += $dependencyId

                $resolvedEdgeCount++

                continue
            }

            if ($nodes.ContainsKey($dependencyId)) {
                $nodes[$sourceId].Dependencies += $dependencyId

                if ($nodes[$dependencyId].RequiredBy -notcontains $sourceId) {
                    $nodes[$dependencyId].RequiredBy += $sourceId
                }

                $mod.ResolvedDependencies += $dependencyId

                $dependencyMod = $nodes[$dependencyId].Mod

                if ($dependencyMod.RequiredBy -notcontains $sourceId) {
                    $dependencyMod.RequiredBy += $sourceId
                }

                $resolvedEdgeCount++
            }
            else {
                $mod.MissingDependencies += $dependencyId

                $missingDependencies += [PSCustomObject]@{
                    RequiredByPackageId = $sourceId
                    RequiredByName      = $mod.Name
                    MissingPackageId    = $dependencyId
                    FolderName          = $mod.FolderName
                    WorkshopID          = $mod.WorkshopID
                }
            }
        }
    }

    # ------------------------------------------------------------
    # Detect cycles
    # ------------------------------------------------------------

    $cycles = @(Find-DependencyCycles -Nodes $nodes)

    # ------------------------------------------------------------
    # Build a dependency-safe order
    # ------------------------------------------------------------

    $loadOrderResult = Get-DependencyLoadOrder -Nodes $nodes

    $graph = [PSCustomObject]@{
        NodeCount               = $nodes.Count
        DeclaredEdgeCount       = $declaredEdgeCount
        ResolvedEdgeCount       = $resolvedEdgeCount
        Nodes                   = $nodes
        MissingDependencies     = @($missingDependencies)
        MissingDependencyCount  = @($missingDependencies).Count
        Cycles                  = @($cycles)
        CycleCount              = @($cycles).Count
        SuggestedLoadOrder      = @($loadOrderResult.Order)
        UnsortablePackageIds    = @($loadOrderResult.Unsortable)
    }

    Write-Log SUCCESS "Dependency graph built."
    Write-Log INFO "Graph nodes             : $($graph.NodeCount)"
    Write-Log INFO "Declared dependencies   : $($graph.DeclaredEdgeCount)"
    Write-Log INFO "Resolved dependencies   : $($graph.ResolvedEdgeCount)"
    Write-Log INFO "Missing dependencies    : $($graph.MissingDependencyCount)"
    Write-Log INFO "Dependency cycles       : $($graph.CycleCount)"
    Write-Log INFO "Suggested order entries : $($graph.SuggestedLoadOrder.Count)"

    return $graph
}

function Find-DependencyCycles {
    <#
    .SYNOPSIS
        Detects circular dependencies using depth-first search.

    .DESCRIPTION
        Uses a shared mutable stack so recursive calls remain compatible
        with Windows PowerShell 5.1 and StrictMode.
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Collections.IDictionary]$Nodes
    )

    $state = @{}

    $stack = New-Object System.Collections.ArrayList
    $cycles = New-Object System.Collections.ArrayList

    $cycleKeys = @{}

    foreach ($packageIdRaw in $Nodes.Keys) {
        $packageId = [string]$packageIdRaw
        $state[$packageId] = 0
    }

    function Visit-DependencyNode {
        [CmdletBinding()]
        param(
            [Parameter(Mandatory)]
            [string]$PackageId
        )

        $state[$PackageId] = 1

        [void]$stack.Add($PackageId)

        foreach ($dependencyIdRaw in @($Nodes[$PackageId].Dependencies)) {
            $dependencyId = ([string]$dependencyIdRaw).Trim()

            if ([string]::IsNullOrWhiteSpace($dependencyId)) {
                continue
            }

            if (-not $Nodes.Contains($dependencyId)) {
                continue
            }

            if ($state[$dependencyId] -eq 0) {
                Visit-DependencyNode -PackageId $dependencyId
            }
            elseif ($state[$dependencyId] -eq 1) {
                $startIndex = $stack.IndexOf($dependencyId)

                if ($startIndex -lt 0) {
                    continue
                }

                $cyclePackageIds = @()

                for (
                    $index = $startIndex
                    $index -lt $stack.Count
                    $index++
                ) {
                    $cyclePackageIds += [string]$stack[$index]
                }

                # Close the cycle visually: A -> B -> C -> A
                $cyclePackageIds += $dependencyId

                $uniqueMembers = @(
                    $cyclePackageIds |
                        Select-Object -Unique |
                        Sort-Object
                )

                $cycleKey = $uniqueMembers -join "|"

                if (-not $cycleKeys.ContainsKey($cycleKey)) {
                    $cycleKeys[$cycleKey] = $true

                    $cycleNames = @(
                        foreach ($id in $cyclePackageIds) {
                            if ($Nodes.Contains($id)) {
                                $Nodes[$id].Name
                            }
                            else {
                                $id
                            }
                        }
                    )

                    $cycleRecord = [PSCustomObject]@{
                        PackageIds = @($cyclePackageIds)
                        Names      = @($cycleNames)
                    }

                    [void]$cycles.Add($cycleRecord)
                }
            }
        }

        if ($stack.Count -gt 0) {
            $stack.RemoveAt($stack.Count - 1)
        }

        $state[$PackageId] = 2
    }

    foreach ($packageIdRaw in @($Nodes.Keys | Sort-Object)) {
        $packageId = [string]$packageIdRaw

        if ($state[$packageId] -eq 0) {
            Visit-DependencyNode -PackageId $packageId
        }
    }

    return @($cycles.ToArray())
}

function Get-DependencyLoadOrder {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Collections.IDictionary]$Nodes
    )

    $inDegree = @{}
    $dependents = @{}

    foreach ($packageIdRaw in $Nodes.Keys) {
        $packageId = [string]$packageIdRaw

        $inDegree[$packageId] = 0
        $dependents[$packageId] = @()
    }

    foreach ($packageIdRaw in $Nodes.Keys) {
        $packageId = [string]$packageIdRaw

        foreach ($dependencyIdRaw in @($Nodes[$packageId].Dependencies)) {
            $dependencyId = [string]$dependencyIdRaw

            if (-not $Nodes.Contains($dependencyId)) {
                continue
            }

            $inDegree[$packageId]++

            if ($dependents[$dependencyId] -notcontains $packageId) {
                $dependents[$dependencyId] += $packageId
            }
        }
    }

    $ready = @(
        foreach ($packageIdRaw in $Nodes.Keys) {
            $packageId = [string]$packageIdRaw

            if ($inDegree[$packageId] -eq 0) {
                $packageId
            }
        }
    ) | Sort-Object

    $order = @()

    while ($ready.Count -gt 0) {
        $current = [string]$ready[0]

        if ($ready.Count -gt 1) {
            $ready = @($ready[1..($ready.Count - 1)])
        }
        else {
            $ready = @()
        }

        $order += $current

        foreach ($dependentIdRaw in @($dependents[$current] | Sort-Object)) {
            $dependentId = [string]$dependentIdRaw
            $inDegree[$dependentId]--

            if ($inDegree[$dependentId] -eq 0) {
                $ready += $dependentId
                $ready = @($ready | Sort-Object -Unique)
            }
        }
    }

    $unsortable = @(
        foreach ($packageIdRaw in $Nodes.Keys) {
            $packageId = [string]$packageIdRaw

            if ($inDegree[$packageId] -gt 0) {
                $packageId
            }
        }
    ) | Sort-Object

    return [PSCustomObject]@{
        Order      = @($order)
        Unsortable = @($unsortable)
    }
}

Export-ModuleMember `
    -Function New-DependencyGraph, Find-DependencyCycles, Get-DependencyLoadOrder