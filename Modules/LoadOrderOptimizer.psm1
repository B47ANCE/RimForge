Set-StrictMode -Version Latest

function ConvertTo-LoadOrderPackageId {
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

function New-LoadOrderModLookup {
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

        $id = ConvertTo-LoadOrderPackageId `
            -PackageId ([string]$mod.PackageId)

        if ($null -ne $id) {
            $lookup[$id] = $mod
        }
    }

    return $lookup
}

function Add-LoadOrderEdge {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Edges,

        [Parameter(Mandatory)]
        [hashtable]$EdgeKeys,

        [Parameter(Mandatory)]
        [string]$From,

        [Parameter(Mandatory)]
        [string]$To,

        [Parameter(Mandatory)]
        [string]$RuleType,

        [Parameter(Mandatory)]
        [bool]$IsHard,

        [Parameter(Mandatory)]
        [string]$DeclaredBy
    )

    if ($From -eq $To) {
        return
    }

    $key = "{0}|{1}|{2}" -f $From, $To, $RuleType

    if ($EdgeKeys.ContainsKey($key)) {
        return
    }

    $EdgeKeys[$key] = $true

    $edge = [PSCustomObject]@{
        From       = $From
        To         = $To
        RuleType   = $RuleType
        IsHard     = $IsHard
        DeclaredBy = $DeclaredBy
    }

    if (-not $Edges.ContainsKey($From)) {
        $Edges[$From] = @()
    }

    $Edges[$From] = @($Edges[$From]) + $edge
}

function Get-ProfileConstraintGraph {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Profile,

        [Parameter(Mandatory)]
        [array]$Mods
    )

    $modLookup = New-LoadOrderModLookup -Mods $Mods
    $activeLookup = @{}
    $positionLookup = @{}
    $displayLookup = @{}

    foreach ($entry in @($Profile.ActiveMods)) {
        $id = [string]$entry.NormalizedPackageId
        $activeLookup[$id] = $true
        $positionLookup[$id] = [int]$entry.Position
        $displayLookup[$id] = [string]$entry.PackageId
    }

    $edges = @{}
    $edgeKeys = @{}

    foreach ($entry in @($Profile.ActiveMods)) {
        $modId = [string]$entry.NormalizedPackageId

        if (-not $modLookup.ContainsKey($modId)) {
            continue
        }

        $mod = $modLookup[$modId]

        foreach ($raw in @($mod.Dependencies)) {
            $dependencyId = ConvertTo-LoadOrderPackageId `
                -PackageId ([string]$raw)

            if (
                $null -ne $dependencyId -and
                $activeLookup.ContainsKey($dependencyId)
            ) {
                Add-LoadOrderEdge `
                    -Edges $edges `
                    -EdgeKeys $edgeKeys `
                    -From $dependencyId `
                    -To $modId `
                    -RuleType "Dependency" `
                    -IsHard $true `
                    -DeclaredBy $modId
            }
        }

        foreach ($raw in @($mod.ForceLoadAfter)) {
            $targetId = ConvertTo-LoadOrderPackageId `
                -PackageId ([string]$raw)

            if (
                $null -ne $targetId -and
                $activeLookup.ContainsKey($targetId)
            ) {
                Add-LoadOrderEdge `
                    -Edges $edges `
                    -EdgeKeys $edgeKeys `
                    -From $targetId `
                    -To $modId `
                    -RuleType "ForceLoadAfter" `
                    -IsHard $true `
                    -DeclaredBy $modId
            }
        }

        foreach ($raw in @($mod.ForceLoadBefore)) {
            $targetId = ConvertTo-LoadOrderPackageId `
                -PackageId ([string]$raw)

            if (
                $null -ne $targetId -and
                $activeLookup.ContainsKey($targetId)
            ) {
                Add-LoadOrderEdge `
                    -Edges $edges `
                    -EdgeKeys $edgeKeys `
                    -From $modId `
                    -To $targetId `
                    -RuleType "ForceLoadBefore" `
                    -IsHard $true `
                    -DeclaredBy $modId
            }
        }

        foreach ($raw in @($mod.LoadAfter)) {
            $targetId = ConvertTo-LoadOrderPackageId `
                -PackageId ([string]$raw)

            if (
                $null -ne $targetId -and
                $activeLookup.ContainsKey($targetId)
            ) {
                Add-LoadOrderEdge `
                    -Edges $edges `
                    -EdgeKeys $edgeKeys `
                    -From $targetId `
                    -To $modId `
                    -RuleType "LoadAfter" `
                    -IsHard $false `
                    -DeclaredBy $modId
            }
        }

        foreach ($raw in @($mod.LoadBefore)) {
            $targetId = ConvertTo-LoadOrderPackageId `
                -PackageId ([string]$raw)

            if (
                $null -ne $targetId -and
                $activeLookup.ContainsKey($targetId)
            ) {
                Add-LoadOrderEdge `
                    -Edges $edges `
                    -EdgeKeys $edgeKeys `
                    -From $modId `
                    -To $targetId `
                    -RuleType "LoadBefore" `
                    -IsHard $false `
                    -DeclaredBy $modId
            }
        }
    }

    return [PSCustomObject]@{
        ActiveLookup   = $activeLookup
        PositionLookup = $positionLookup
        DisplayLookup  = $displayLookup
        Edges          = $edges
    }
}

function Invoke-StableTopologicalSort {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$Nodes,

        [Parameter(Mandatory)]
        [hashtable]$PositionLookup,

        [Parameter(Mandatory)]
        [hashtable]$Edges,

        [Parameter(Mandatory)]
        [bool]$IncludeSoftEdges
    )

    $inDegree = @{}
    $outgoing = @{}

    foreach ($node in @($Nodes)) {
        $inDegree[$node] = 0
        $outgoing[$node] = @()
    }

    foreach ($from in @($Edges.Keys)) {
        foreach ($edge in @($Edges[$from])) {
            if (-not $IncludeSoftEdges -and -not $edge.IsHard) {
                continue
            }

            if (
                -not $inDegree.ContainsKey($edge.From) -or
                -not $inDegree.ContainsKey($edge.To)
            ) {
                continue
            }

            $outgoing[$edge.From] = @($outgoing[$edge.From]) + $edge
            $inDegree[$edge.To]++
        }
    }

    $ready = @(
        foreach ($node in @($Nodes)) {
            if ($inDegree[$node] -eq 0) {
                $node
            }
        }
    )

    $order = @()

    while (@($ready).Count -gt 0) {
        $ready = @(
            $ready |
            Sort-Object @{
                Expression = {
                    if ($PositionLookup.ContainsKey($_)) {
                        $PositionLookup[$_]
                    }
                    else {
                        [int]::MaxValue
                    }
                }
            }, @{
                Expression = { $_ }
            }
        )

        $current = [string]$ready[0]

        if (@($ready).Count -gt 1) {
            $ready = @($ready[1..(@($ready).Count - 1)])
        }
        else {
            $ready = @()
        }

        $order += $current

        foreach ($edge in @($outgoing[$current])) {
            $inDegree[$edge.To]--

            if ($inDegree[$edge.To] -eq 0) {
                $ready += $edge.To
            }
        }
    }

    $remaining = @(
        foreach ($node in @($Nodes)) {
            if ($inDegree[$node] -gt 0) {
                $node
            }
        }
    )

    return [PSCustomObject]@{
        Order     = @($order)
        Remaining = @($remaining)
        Complete  = (@($remaining).Count -eq 0)
    }
}

function Get-HardStronglyConnectedComponents {
    <#
    .SYNOPSIS
        Finds strongly connected components in the active hard-rule graph.

    .DESCRIPTION
        Uses Kosaraju's algorithm. Components with more than one member are
        hard dependency/load-order cycles and must be treated as one unit.
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$Nodes,

        [Parameter(Mandatory)]
        [hashtable]$Edges,

        [Parameter(Mandatory)]
        [hashtable]$PositionLookup
    )

    $adjacency = @{}
    $reverseAdjacency = @{}

    foreach ($node in @($Nodes)) {
        $adjacency[$node] = @()
        $reverseAdjacency[$node] = @()
    }

    foreach ($from in @($Edges.Keys)) {
        foreach ($edge in @($Edges[$from])) {
            if (-not $edge.IsHard) {
                continue
            }

            if (
                -not $adjacency.ContainsKey($edge.From) -or
                -not $adjacency.ContainsKey($edge.To)
            ) {
                continue
            }

            if (@($adjacency[$edge.From]) -notcontains $edge.To) {
                $adjacency[$edge.From] = @($adjacency[$edge.From]) + $edge.To
            }

            if (@($reverseAdjacency[$edge.To]) -notcontains $edge.From) {
                $reverseAdjacency[$edge.To] =
                    @($reverseAdjacency[$edge.To]) + $edge.From
            }
        }
    }

    # First pass: calculate DFS finish order without recursive functions.
    $visited = @{}
    $finishOrder = New-Object System.Collections.ArrayList

    foreach ($startNode in @(
        $Nodes |
        Sort-Object @{
            Expression = { $PositionLookup[$_] }
        }
    )) {
        if ($visited.ContainsKey($startNode)) {
            continue
        }

        $stack = New-Object System.Collections.Stack
        $stack.Push([PSCustomObject]@{
            Node     = $startNode
            Expanded = $false
        })

        while ($stack.Count -gt 0) {
            $frame = $stack.Pop()
            $node = [string]$frame.Node

            if ($frame.Expanded) {
                [void]$finishOrder.Add($node)
                continue
            }

            if ($visited.ContainsKey($node)) {
                continue
            }

            $visited[$node] = $true

            $stack.Push([PSCustomObject]@{
                Node     = $node
                Expanded = $true
            })

            $neighbors = @(
                $adjacency[$node] |
                Sort-Object @{
                    Expression = { $PositionLookup[$_] }
                    Descending = $true
                }
            )

            foreach ($neighbor in @($neighbors)) {
                if (-not $visited.ContainsKey($neighbor)) {
                    $stack.Push([PSCustomObject]@{
                        Node     = $neighbor
                        Expanded = $false
                    })
                }
            }
        }
    }

    # Second pass: walk the reversed graph in reverse finish order.
    $assigned = @{}
    $components = @()
    $componentIndex = 0

    for (
        $index = $finishOrder.Count - 1;
        $index -ge 0;
        $index--
    ) {
        $startNode = [string]$finishOrder[$index]

        if ($assigned.ContainsKey($startNode)) {
            continue
        }

        $members = New-Object System.Collections.ArrayList
        $stack = New-Object System.Collections.Stack
        $stack.Push($startNode)
        $assigned[$startNode] = $true

        while ($stack.Count -gt 0) {
            $node = [string]$stack.Pop()
            [void]$members.Add($node)

            foreach ($neighbor in @($reverseAdjacency[$node])) {
                if (-not $assigned.ContainsKey($neighbor)) {
                    $assigned[$neighbor] = $true
                    $stack.Push($neighbor)
                }
            }
        }

        $orderedMembers = @(
            $members.ToArray() |
            Sort-Object @{
                Expression = { $PositionLookup[$_] }
            }
        )

        $components += [PSCustomObject]@{
            Id               = "component-$componentIndex"
            Members          = @($orderedMembers)
            OriginalPosition = $PositionLookup[$orderedMembers[0]]
            IsCycle          = (@($orderedMembers).Count -gt 1)
        }

        $componentIndex++
    }

    return @($components)
}

function Get-CondensedHardOrder {
    <#
    .SYNOPSIS
        Collapses hard cycles and sorts the resulting acyclic component graph.
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$Nodes,

        [Parameter(Mandatory)]
        [hashtable]$Edges,

        [Parameter(Mandatory)]
        [hashtable]$PositionLookup,

        [Parameter(Mandatory)]
        [hashtable]$DisplayLookup
    )

    $components = Get-HardStronglyConnectedComponents `
        -Nodes $Nodes `
        -Edges $Edges `
        -PositionLookup $PositionLookup

    $componentByNode = @{}
    $componentPosition = @{}

    foreach ($component in @($components)) {
        $componentPosition[$component.Id] = $component.OriginalPosition

        foreach ($member in @($component.Members)) {
            $componentByNode[$member] = $component.Id
        }
    }

    $componentEdges = @{}
    $componentEdgeKeys = @{}

    foreach ($component in @($components)) {
        $componentEdges[$component.Id] = @()
    }

    foreach ($from in @($Edges.Keys)) {
        foreach ($edge in @($Edges[$from])) {
            if (-not $edge.IsHard) {
                continue
            }

            if (
                -not $componentByNode.ContainsKey($edge.From) -or
                -not $componentByNode.ContainsKey($edge.To)
            ) {
                continue
            }

            $fromComponent = $componentByNode[$edge.From]
            $toComponent = $componentByNode[$edge.To]

            if ($fromComponent -eq $toComponent) {
                continue
            }

            $key = "$fromComponent|$toComponent"

            if (-not $componentEdgeKeys.ContainsKey($key)) {
                $componentEdgeKeys[$key] = $true

                $componentEdges[$fromComponent] =
                    @($componentEdges[$fromComponent]) +
                    [PSCustomObject]@{
                        From       = $fromComponent
                        To         = $toComponent
                        RuleType   = "CondensedHardRule"
                        IsHard     = $true
                        DeclaredBy = $edge.DeclaredBy
                    }
            }
        }
    }

    $componentIds = @(
        $components |
        Sort-Object OriginalPosition |
        ForEach-Object { $_.Id }
    )

    $sortedComponents = Invoke-StableTopologicalSort `
        -Nodes $componentIds `
        -PositionLookup $componentPosition `
        -Edges $componentEdges `
        -IncludeSoftEdges $false

    if (-not $sortedComponents.Complete) {
        throw "Condensed hard-rule graph unexpectedly contains a cycle."
    }

    $componentLookup = @{}

    foreach ($component in @($components)) {
        $componentLookup[$component.Id] = $component
    }

    $targetOrder = @()

    foreach ($componentId in @($sortedComponents.Order)) {
        $targetOrder += @($componentLookup[$componentId].Members)
    }

    $cycleGroups = @()

    foreach ($component in @(
        $components |
        Where-Object { $_.IsCycle }
    )) {
        $memberLookup = @{}

        foreach ($member in @($component.Members)) {
            $memberLookup[$member] = $true
        }

        $internalRules = @()

        foreach ($from in @($Edges.Keys)) {
            foreach ($edge in @($Edges[$from])) {
                if (
                    $edge.IsHard -and
                    $memberLookup.ContainsKey($edge.From) -and
                    $memberLookup.ContainsKey($edge.To)
                ) {
                    $internalRules += [PSCustomObject]@{
                        RuleType = $edge.RuleType
                        From     = $DisplayLookup[$edge.From]
                        To       = $DisplayLookup[$edge.To]
                    }
                }
            }
        }

        $cycleGroups += [PSCustomObject]@{
            Members = @(
                foreach ($member in @($component.Members)) {
                    $DisplayLookup[$member]
                }
            )
            InternalRules  = @($internalRules)
            Recommendation = (
                "Automatic ordering is impossible because these mods form " +
                "a hard cycle. Their existing relative order was preserved."
            )
        }
    }

    return [PSCustomObject]@{
        TargetOrder  = @($targetOrder)
        Components   = @($components)
        CycleGroups  = @($cycleGroups)
    }
}

function Get-LongestIncreasingSubsequenceIndexes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int[]]$Values
    )

    if (@($Values).Count -eq 0) {
        return @()
    }

    $lengths = New-Object int[] @($Values).Count
    $previous = New-Object int[] @($Values).Count

    for ($i = 0; $i -lt @($Values).Count; $i++) {
        $lengths[$i] = 1
        $previous[$i] = -1

        for ($j = 0; $j -lt $i; $j++) {
            if (
                $Values[$j] -lt $Values[$i] -and
                ($lengths[$j] + 1) -gt $lengths[$i]
            ) {
                $lengths[$i] = $lengths[$j] + 1
                $previous[$i] = $j
            }
        }
    }

    $bestIndex = 0

    for ($i = 1; $i -lt @($Values).Count; $i++) {
        if ($lengths[$i] -gt $lengths[$bestIndex]) {
            $bestIndex = $i
        }
    }

    $indexes = @()
    $cursor = $bestIndex

    while ($cursor -ge 0) {
        $indexes = @($cursor) + $indexes
        $cursor = $previous[$cursor]
    }

    return @($indexes)
}

function Get-MinimalLoadOrderActions {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$OriginalOrder,

        [Parameter(Mandatory)]
        [string[]]$TargetOrder,

        [Parameter(Mandatory)]
        [hashtable]$DisplayLookup,

        [Parameter(Mandatory)]
        [hashtable]$Edges,

        [Parameter(Mandatory)]
        [array]$CycleGroups
    )

    $originalPosition = @{}

    for ($i = 0; $i -lt @($OriginalOrder).Count; $i++) {
        $originalPosition[$OriginalOrder[$i]] = $i
    }

    $positionSequence = @(
        foreach ($id in @($TargetOrder)) {
            $originalPosition[$id]
        }
    )

    $lisIndexes = Get-LongestIncreasingSubsequenceIndexes `
        -Values @($positionSequence)

    $keepLookup = @{}

    foreach ($index in @($lisIndexes)) {
        $keepLookup[$TargetOrder[$index]] = $true
    }

    $cycleMemberLookup = @{}

    foreach ($cycle in @($CycleGroups)) {
        foreach ($displayId in @($cycle.Members)) {
            $normalized = ConvertTo-LoadOrderPackageId `
                -PackageId ([string]$displayId)

            if ($null -ne $normalized) {
                $cycleMemberLookup[$normalized] = $true
            }
        }
    }

    $working = @($OriginalOrder)
    $actions = @()

    for (
        $targetIndex = 0;
        $targetIndex -lt @($TargetOrder).Count;
        $targetIndex++
    ) {
        $id = [string]$TargetOrder[$targetIndex]

        if ($keepLookup.ContainsKey($id)) {
            continue
        }

        # Members of a hard dependency cycle have no valid automatic
        # before/after solution. Preserve them and never emit move actions.
        if ($cycleMemberLookup.ContainsKey($id)) {
            continue
        }

        $currentIndex = [array]::IndexOf($working, $id)

        if ($currentIndex -lt 0 -or $currentIndex -eq $targetIndex) {
            continue
        }

        $workingList = New-Object System.Collections.ArrayList
        [void]$workingList.AddRange([object[]]$working)
        $workingList.RemoveAt($currentIndex)

        if ($targetIndex -ge $workingList.Count) {
            [void]$workingList.Add($id)
        }
        else {
            $workingList.Insert($targetIndex, $id)
        }

        $working = @($workingList.ToArray())

        $beforeId = if ($targetIndex -lt (@($TargetOrder).Count - 1)) {
            [string]$TargetOrder[$targetIndex + 1]
        }
        else {
            $null
        }

        $afterId = if ($targetIndex -gt 0) {
            [string]$TargetOrder[$targetIndex - 1]
        }
        else {
            $null
        }

        $requiredAfter = @()
        $requiredBefore = @()
        $forcedAfter = @()
        $forcedBefore = @()

        foreach ($from in @($Edges.Keys)) {
            foreach ($edge in @($Edges[$from])) {
                if (-not $edge.IsHard) {
                    continue
                }

                if ($edge.RuleType -eq "Dependency") {
                    if ($edge.To -eq $id) {
                        $requiredAfter += $DisplayLookup[$edge.From]
                    }
                    elseif ($edge.From -eq $id) {
                        $requiredBefore += $DisplayLookup[$edge.To]
                    }
                }
                elseif ($edge.RuleType -eq "ForceLoadAfter") {
                    if ($edge.To -eq $id) {
                        $forcedAfter += $DisplayLookup[$edge.From]
                    }
                    elseif ($edge.From -eq $id) {
                        $forcedBefore += $DisplayLookup[$edge.To]
                    }
                }
                elseif ($edge.RuleType -eq "ForceLoadBefore") {
                    if ($edge.From -eq $id) {
                        $forcedBefore += $DisplayLookup[$edge.To]
                    }
                    elseif ($edge.To -eq $id) {
                        $forcedAfter += $DisplayLookup[$edge.From]
                    }
                }
                elseif (
                    $edge.RuleType -eq "KnowledgeMustLoadAfter" -or
                    $edge.RuleType -eq "KnowledgeMustBeLast"
                ) {
                    if ($edge.To -eq $id) {
                        $forcedAfter += $DisplayLookup[$edge.From]
                    }
                }
                elseif ($edge.RuleType -eq "KnowledgeMustBeFirst") {
                    if ($edge.From -eq $id) {
                        $forcedBefore += $DisplayLookup[$edge.To]
                    }
                }
                elseif ($edge.RuleType -eq "KnowledgeMustLoadBefore") {
                    if ($edge.From -eq $id) {
                        $forcedBefore += $DisplayLookup[$edge.To]
                    }
                }
            }
        }

        $requiredAfter = @($requiredAfter | Sort-Object -Unique)
        $requiredBefore = @($requiredBefore | Sort-Object -Unique)
        $forcedAfter = @($forcedAfter | Sort-Object -Unique)
        $forcedBefore = @($forcedBefore | Sort-Object -Unique)

        $summaryParts = @()

        if (@($requiredAfter).Count -gt 0) {
            $summaryParts += (
                "Must load after required dependencies: {0}" -f
                ($requiredAfter -join ", ")
            )
        }

        if (@($requiredBefore).Count -gt 0) {
            $summaryParts += (
                "Must load before dependent mods: {0}" -f
                ($requiredBefore -join ", ")
            )
        }

        if (@($forcedAfter).Count -gt 0) {
            $summaryParts += (
                "Forced to load after: {0}" -f
                ($forcedAfter -join ", ")
            )
        }

        if (@($forcedBefore).Count -gt 0) {
            $summaryParts += (
                "Forced to load before: {0}" -f
                ($forcedBefore -join ", ")
            )
        }

        $actions += [PSCustomObject]@{
            PackageId    = $DisplayLookup[$id]
            FromPosition = $currentIndex
            ToPosition   = $targetIndex
            MoveAfter    = if ($null -ne $afterId) {
                $DisplayLookup[$afterId]
            }
            else {
                $null
            }
            MoveBefore   = if ($null -ne $beforeId) {
                $DisplayLookup[$beforeId]
            }
            else {
                $null
            }
            ReasonSummary = [PSCustomObject]@{
                RequiredAfter = @($requiredAfter)
                RequiredBefore = @($requiredBefore)
                ForcedAfter = @($forcedAfter)
                ForcedBefore = @($forcedBefore)
                Explanation = $summaryParts -join "; "
            }
        }
    }

    return [PSCustomObject]@{
        ActionCount = @($actions).Count
        Actions     = @($actions)
        FinalOrder  = @($working)
    }
}

function Optimize-ModsConfigLoadOrder {
    <#
    .SYNOPSIS
        Produces a conservative optimized order with hard cycles collapsed.

    .DESCRIPTION
        Required dependencies and forceLoad rules are enforced. Strongly
        connected components are treated as single graph nodes, and their
        existing internal order is preserved. Ordinary loadBefore/loadAfter
        declarations remain non-automatic recommendations.
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Profile,

        [Parameter(Mandatory)]
        [array]$Mods,

        $KnowledgeRules = $null
    )

    $graph = Get-ProfileConstraintGraph `
        -Profile $Profile `
        -Mods $Mods

    # Apply curated hard rules from Database\LoadOrderRules.json.
    if ($null -ne $KnowledgeRules) {
        $activeIds = @{}

        foreach ($entry in @($Profile.ActiveMods)) {
            $activeIds[$entry.NormalizedPackageId] = $true
        }

        $knowledgeEdgeKeys = @{}

        foreach ($from in @($graph.Edges.Keys)) {
            foreach ($edge in @($graph.Edges[$from])) {
                $knowledgeEdgeKeys[
                    "{0}|{1}|{2}" -f
                    $edge.From,
                    $edge.To,
                    $edge.RuleType
                ] = $true
            }
        }

        foreach ($rule in @($KnowledgeRules.Rules)) {
            $ruleId = ConvertTo-LoadOrderPackageId `
                -PackageId ([string]$rule.PackageId)

            if (
                $null -eq $ruleId -or
                -not $activeIds.ContainsKey($ruleId)
            ) {
                continue
            }

            $profileFilters = @($rule.AppliesToProfiles)

            if (
                @($profileFilters).Count -gt 0 -and
                $profileFilters -notcontains $Profile.Name
            ) {
                continue
            }

            foreach ($rawAfter in @($rule.MustLoadAfter)) {
                $afterId = ConvertTo-LoadOrderPackageId `
                    -PackageId ([string]$rawAfter)

                if (
                    $null -ne $afterId -and
                    $activeIds.ContainsKey($afterId)
                ) {
                    Add-LoadOrderEdge `
                        -Edges $graph.Edges `
                        -EdgeKeys $knowledgeEdgeKeys `
                        -From $afterId `
                        -To $ruleId `
                        -RuleType "KnowledgeMustLoadAfter" `
                        -IsHard $true `
                        -DeclaredBy $ruleId
                }
            }

            foreach ($rawBefore in @($rule.MustLoadBefore)) {
                $beforeId = ConvertTo-LoadOrderPackageId `
                    -PackageId ([string]$rawBefore)

                if (
                    $null -ne $beforeId -and
                    $activeIds.ContainsKey($beforeId)
                ) {
                    Add-LoadOrderEdge `
                        -Edges $graph.Edges `
                        -EdgeKeys $knowledgeEdgeKeys `
                        -From $ruleId `
                        -To $beforeId `
                        -RuleType "KnowledgeMustLoadBefore" `
                        -IsHard $true `
                        -DeclaredBy $ruleId
                }
            }

            $mustBeFirst = (
                $rule.PSObject.Properties.Name -contains "MustBeFirst" -and
                [bool]$rule.MustBeFirst
            )

            if ($mustBeFirst) {
                # All first-position mods are kept together at the beginning.
                # When more than one is active, preserve their existing order.
                $firstIds = @(
                    foreach ($candidate in @($KnowledgeRules.Rules)) {
                        $candidateId = ConvertTo-LoadOrderPackageId `
                            -PackageId ([string]$candidate.PackageId)

                        if (
                            $null -ne $candidateId -and
                            $activeIds.ContainsKey($candidateId) -and
                            $candidate.PSObject.Properties.Name -contains "MustBeFirst" -and
                            [bool]$candidate.MustBeFirst
                        ) {
                            $candidateId
                        }
                    }
                )

                foreach ($otherId in @($activeIds.Keys)) {
                    if ($firstIds -contains $otherId) {
                        continue
                    }

                    Add-LoadOrderEdge `
                        -Edges $graph.Edges `
                        -EdgeKeys $knowledgeEdgeKeys `
                        -From $ruleId `
                        -To $otherId `
                        -RuleType "KnowledgeMustBeFirst" `
                        -IsHard $true `
                        -DeclaredBy $ruleId
                }
            }

            $mustBeLast = (
                $rule.PSObject.Properties.Name -contains "MustBeLast" -and
                [bool]$rule.MustBeLast
            )

            if ($mustBeLast) {
                # All terminal mods are kept together at the end. When more
                # than one is active, preserve their existing relative order.
                $terminalIds = @(
                    foreach ($candidate in @($KnowledgeRules.Rules)) {
                        $candidateId = ConvertTo-LoadOrderPackageId `
                            -PackageId ([string]$candidate.PackageId)

                        if (
                            $null -ne $candidateId -and
                            $activeIds.ContainsKey($candidateId) -and
                            $candidate.PSObject.Properties.Name -contains "MustBeLast" -and
                            [bool]$candidate.MustBeLast
                        ) {
                            $candidateId
                        }
                    }
                )

                foreach ($otherId in @($activeIds.Keys)) {
                    if ($terminalIds -contains $otherId) {
                        continue
                    }

                    Add-LoadOrderEdge `
                        -Edges $graph.Edges `
                        -EdgeKeys $knowledgeEdgeKeys `
                        -From $otherId `
                        -To $ruleId `
                        -RuleType "KnowledgeMustBeLast" `
                        -IsHard $true `
                        -DeclaredBy $ruleId
                }
            }
        }
    }

    $nodes = @(
        foreach ($entry in @($Profile.ActiveMods)) {
            [string]$entry.NormalizedPackageId
        }
    )

    $condensed = Get-CondensedHardOrder `
        -Nodes $nodes `
        -Edges $graph.Edges `
        -PositionLookup $graph.PositionLookup `
        -DisplayLookup $graph.DisplayLookup

    $targetNormalizedOrder = @($condensed.TargetOrder)

    $minimal = Get-MinimalLoadOrderActions `
        -OriginalOrder @($nodes) `
        -TargetOrder @($targetNormalizedOrder) `
        -DisplayLookup $graph.DisplayLookup `
        -Edges $graph.Edges `
        -CycleGroups @($condensed.CycleGroups)

    # Use the order actually produced by the conservative minimal-action
    # pass. This can intentionally differ from the graph target when hard
    # cycle members were frozen in place.
    $suggestedPackageIds = @(
        foreach ($id in @($minimal.FinalOrder)) {
            if ($graph.DisplayLookup.ContainsKey($id)) {
                $graph.DisplayLookup[$id]
            }
            else {
                $id
            }
        }
    )

    $softRecommendations = @()

    foreach ($from in @($graph.Edges.Keys)) {
        foreach ($edge in @($graph.Edges[$from])) {
            if ($edge.IsHard) {
                continue
            }

            if (
                $graph.PositionLookup[$edge.From] -gt
                $graph.PositionLookup[$edge.To]
            ) {
                $softRecommendations += [PSCustomObject]@{
                    RuleType = $edge.RuleType
                    PackageId = $graph.DisplayLookup[$edge.DeclaredBy]
                    ShouldLoadAfter = if ($edge.RuleType -eq "LoadAfter") {
                        $graph.DisplayLookup[$edge.From]
                    }
                    else {
                        $null
                    }
                    ShouldLoadBefore = if ($edge.RuleType -eq "LoadBefore") {
                        $graph.DisplayLookup[$edge.To]
                    }
                    else {
                        $null
                    }
                    Explanation = if ($edge.RuleType -eq "LoadAfter") {
                        "{0} recommends loading after {1}." -f
                        $graph.DisplayLookup[$edge.DeclaredBy],
                        $graph.DisplayLookup[$edge.From]
                    }
                    else {
                        "{0} recommends loading before {1}." -f
                        $graph.DisplayLookup[$edge.DeclaredBy],
                        $graph.DisplayLookup[$edge.To]
                    }
                }
            }
        }
    }

    return [PSCustomObject]@{
        ProfileName          = $Profile.Name
        OriginalCount        = $Profile.Count
        SuggestedPackageIds  = @($suggestedPackageIds)
        MoveCount            = $minimal.ActionCount
        Actions              = @($minimal.Actions)
        SoftRecommendations  = @($softRecommendations)
        CycleGroups          = @($condensed.CycleGroups)
        HardCycleMembers     = @(
            foreach ($cycle in @($condensed.CycleGroups)) {
                foreach ($member in @($cycle.Members)) {
                    $member
                }
            }
        )
    }
}

function Write-OptimizedModsConfigProfile {
    <#
    .SYNOPSIS
        Writes an optimized profile in RimWorld's native ModsConfig.xml format.

    .DESCRIPTION
        Always writes native XML, even when the source profile was imported
        from a RimPy/RimSort JSON export stored with an .xml extension.
        The source profile is never overwritten.
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Profile,

        [Parameter(Mandatory)]
        $Optimization,

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

    $safeName = $Profile.Name -replace '[\\/:*?"<>|]', "_"

    # Use a native RimWorld-style filename and XML structure.
    $outputPath = Join-Path `
        $OutputFolder `
        ("{0}.ModsConfig.xml" -f $safeName)

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.IndentChars = "  "
    $settings.NewLineChars = [Environment]::NewLine
    $settings.NewLineHandling = `
        [System.Xml.NewLineHandling]::Replace
    $settings.OmitXmlDeclaration = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)

    $writer = [System.Xml.XmlWriter]::Create(
        $outputPath,
        $settings
    )

    try {
        $writer.WriteStartElement("ModsConfigData")

        $writer.WriteElementString(
            "version",
            [string]$Profile.Version
        )

        $writer.WriteStartElement("activeMods")

        foreach ($packageId in @(
            $Optimization.SuggestedPackageIds
        )) {
            $writer.WriteElementString(
                "li",
                [string]$packageId
            )
        }

        $writer.WriteEndElement()

        $writer.WriteStartElement("knownExpansions")

        foreach ($packageId in @($Profile.KnownExpansions)) {
            $writer.WriteElementString(
                "li",
                [string]$packageId
            )
        }

        $writer.WriteEndElement()
        $writer.WriteEndElement()
    }
    finally {
        $writer.Dispose()
    }

    $reportPath = Join-Path `
        $OutputFolder `
        ("{0}.optimization.json" -f $safeName)

    $actionsPath = Join-Path `
        $OutputFolder `
        ("{0}.LoadOrderActions.json" -f $safeName)

    $Optimization |
        ConvertTo-Json -Depth 12 |
        Set-Content `
            -LiteralPath $reportPath `
            -Encoding UTF8

    [PSCustomObject]@{
        ProfileName         = $Optimization.ProfileName
        ActionCount         = $Optimization.MoveCount
        Actions             = @($Optimization.Actions)
        SoftRecommendations = @($Optimization.SoftRecommendations)
        CycleGroups         = @($Optimization.CycleGroups)
        HardCycleMembers    = @($Optimization.HardCycleMembers)
    } |
        ConvertTo-Json -Depth 12 |
        Set-Content `
            -LiteralPath $actionsPath `
            -Encoding UTF8

    Write-Log SUCCESS (
        "Native RimWorld ModsConfig written to {0}" -f
        $outputPath
    )

    Write-Log INFO (
        "{0}: {1} minimal load-order action(s), {2} soft recommendation(s)." -f
        $Profile.Name,
        $Optimization.MoveCount,
        @($Optimization.SoftRecommendations).Count
    )

    return [PSCustomObject]@{
        ProfilePath = $outputPath
        ReportPath  = $reportPath
        ActionsPath = $actionsPath
    }
}

Export-ModuleMember -Function `
    Optimize-ModsConfigLoadOrder,
    Write-OptimizedModsConfigProfile
