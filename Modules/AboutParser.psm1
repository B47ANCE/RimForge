Set-StrictMode -Version Latest

function Get-NodeText {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Xml.XmlNode]$ParentNode,

        [Parameter(Mandatory)]
        [string]$ChildName
    )

    $node = $ParentNode.SelectSingleNode($ChildName)

    if ($null -eq $node) {
        return $null
    }

    $value = [string]$node.InnerText

    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value.Trim()
}

function Get-NodeTextArray {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Xml.XmlNode]$ParentNode,

        [Parameter(Mandatory)]
        [string]$XPath
    )

    $values = @()
    $nodes = $ParentNode.SelectNodes($XPath)

    if ($null -eq $nodes) {
        return @()
    }

    foreach ($node in $nodes) {
        $value = [string]$node.InnerText

        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $trimmed = $value.Trim()

            if ($values -notcontains $trimmed) {
                $values += $trimmed
            }
        }
    }

    return @($values)
}

function Get-DependencyRecords {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Xml.XmlNode]$MetadataNode,

        [Parameter(Mandatory)]
        [string]$XPath,

        [string]$Source = "modDependencies"
    )

    $records = @()
    $dependencyNodes = $MetadataNode.SelectNodes($XPath)

    if ($null -eq $dependencyNodes) {
        return @()
    }

    foreach ($dependencyNode in $dependencyNodes) {
        $packageIdNode = $dependencyNode.SelectSingleNode("packageId")

        if ($null -eq $packageIdNode) {
            continue
        }

        $packageId = ([string]$packageIdNode.InnerText).Trim()

        if ([string]::IsNullOrWhiteSpace($packageId)) {
            continue
        }

        $displayName = $null
        $displayNameNode = $dependencyNode.SelectSingleNode("displayName")

        if ($null -ne $displayNameNode) {
            $displayName = ([string]$displayNameNode.InnerText).Trim()
        }

        $steamWorkshopUrl = $null
        $steamWorkshopUrlNode =
            $dependencyNode.SelectSingleNode("steamWorkshopUrl")

        if ($null -ne $steamWorkshopUrlNode) {
            $steamWorkshopUrl =
                ([string]$steamWorkshopUrlNode.InnerText).Trim()
        }

        $downloadUrl = $null
        $downloadUrlNode = $dependencyNode.SelectSingleNode("downloadUrl")

        if ($null -ne $downloadUrlNode) {
            $downloadUrl = ([string]$downloadUrlNode.InnerText).Trim()
        }

        $records += [PSCustomObject]@{
            PackageId        = $packageId
            DisplayName      = $displayName
            SteamWorkshopUrl = $steamWorkshopUrl
            DownloadUrl      = $downloadUrl
            Source           = $Source
        }
    }

    return @($records)
}

function Get-VersionSpecificDependencies {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Xml.XmlNode]$MetadataNode
    )

    $results = @()
    $container = $MetadataNode.SelectSingleNode("modDependenciesByVersion")

    if ($null -eq $container) {
        return @()
    }

    foreach ($versionNode in @($container.ChildNodes)) {
        if ($versionNode.NodeType -ne
            [System.Xml.XmlNodeType]::Element) {
            continue
        }

        $version = [string]$versionNode.Name
        $records = @()

        foreach ($dependencyNode in @($versionNode.SelectNodes("li"))) {
            $packageIdNode = $dependencyNode.SelectSingleNode("packageId")

            if ($null -eq $packageIdNode) {
                continue
            }

            $packageId = ([string]$packageIdNode.InnerText).Trim()

            if ([string]::IsNullOrWhiteSpace($packageId)) {
                continue
            }

            $displayName = $null
            $displayNameNode =
                $dependencyNode.SelectSingleNode("displayName")

            if ($null -ne $displayNameNode) {
                $displayName =
                    ([string]$displayNameNode.InnerText).Trim()
            }

            $records += [PSCustomObject]@{
                PackageId   = $packageId
                DisplayName = $displayName
            }
        }

        $results += [PSCustomObject]@{
            Version      = $version
            Dependencies = @($records)
        }
    }

    return @($results)
}

function Get-AboutMetadata {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Mod
    )

    if (-not (
        Test-Path -LiteralPath $Mod.AboutPath -PathType Leaf
    )) {
        $Mod.Errors = @($Mod.Errors) + "Missing About.xml"
        return $Mod
    }

    try {
        # XmlDocument.Load respects the file's BOM and XML encoding
        # declaration, unlike forcing every file to UTF-8.
        $xml = New-Object System.Xml.XmlDocument
        $xml.PreserveWhitespace = $false
        $xml.Load($Mod.AboutPath)
    }
    catch {
        $Mod.Errors = @($Mod.Errors) + (
            "Invalid About.xml: {0}" -f $_.Exception.Message
        )

        return $Mod
    }

    $metadata = $xml.SelectSingleNode("/ModMetaData")

    if ($null -eq $metadata) {
        $Mod.Errors = @($Mod.Errors) +
            "About.xml does not contain a ModMetaData root node"

        return $Mod
    }

    $name = Get-NodeText `
        -ParentNode $metadata `
        -ChildName "name"

    $packageId = Get-NodeText `
        -ParentNode $metadata `
        -ChildName "packageId"

    $author = Get-NodeText `
        -ParentNode $metadata `
        -ChildName "author"

    $supportedVersions = @(
        Get-NodeTextArray `
            -ParentNode $metadata `
            -XPath "supportedVersions/li"
    )

    if ($supportedVersions.Count -eq 0) {
        $targetVersion = Get-NodeText `
            -ParentNode $metadata `
            -ChildName "targetVersion"

        if (-not [string]::IsNullOrWhiteSpace($targetVersion)) {
            $supportedVersions = @($targetVersion)
        }
    }

    $dependencyRecords = @(
        Get-DependencyRecords `
            -MetadataNode $metadata `
            -XPath "modDependencies/li"
    )

    $dependencies = @(
        foreach ($record in $dependencyRecords) {
            $record.PackageId
        }
    )

    $loadBefore = @(
        Get-NodeTextArray `
            -ParentNode $metadata `
            -XPath "loadBefore/li"
    )

    $loadAfter = @(
        Get-NodeTextArray `
            -ParentNode $metadata `
            -XPath "loadAfter/li"
    )

    $forceLoadBefore = @(
        Get-NodeTextArray `
            -ParentNode $metadata `
            -XPath "forceLoadBefore/li"
    )

    $forceLoadAfter = @(
        Get-NodeTextArray `
            -ParentNode $metadata `
            -XPath "forceLoadAfter/li"
    )

    $incompatibleWith = @(
        Get-NodeTextArray `
            -ParentNode $metadata `
            -XPath "incompatibleWith/li"
    )

    $versionSpecificDependencies = @(
        Get-VersionSpecificDependencies `
            -MetadataNode $metadata
    )

    $Mod.Name = $name
    $Mod.PackageId = $packageId
    $Mod.Author = $author

    $Mod.Dependencies = @($dependencies)
    $Mod.LoadBefore = @($loadBefore)
    $Mod.LoadAfter = @($loadAfter)

    $Mod | Add-Member `
        -NotePropertyName SupportedVersions `
        -NotePropertyValue @($supportedVersions) `
        -Force

    $Mod | Add-Member `
        -NotePropertyName DependencyRecords `
        -NotePropertyValue @($dependencyRecords) `
        -Force

    $Mod | Add-Member `
        -NotePropertyName VersionSpecificDependencies `
        -NotePropertyValue @($versionSpecificDependencies) `
        -Force

    $Mod | Add-Member `
        -NotePropertyName ForceLoadBefore `
        -NotePropertyValue @($forceLoadBefore) `
        -Force

    $Mod | Add-Member `
        -NotePropertyName ForceLoadAfter `
        -NotePropertyValue @($forceLoadAfter) `
        -Force

    $Mod | Add-Member `
        -NotePropertyName IncompatibleWith `
        -NotePropertyValue @($incompatibleWith) `
        -Force

    $Mod | Add-Member `
        -NotePropertyName ModVersion `
        -NotePropertyValue (
            Get-NodeText `
                -ParentNode $metadata `
                -ChildName "modVersion"
        ) `
        -Force

    $Mod | Add-Member `
        -NotePropertyName Url `
        -NotePropertyValue (
            Get-NodeText `
                -ParentNode $metadata `
                -ChildName "url"
        ) `
        -Force

    $Mod | Add-Member `
        -NotePropertyName Description `
        -NotePropertyValue (
            Get-NodeText `
                -ParentNode $metadata `
                -ChildName "description"
        ) `
        -Force

    return $Mod
}

function Set-RimForgeAboutSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Mod,[Parameter(Mandatory)]$Snapshot)

    foreach ($name in @(
        'Name','PackageId','Author','Dependencies','LoadBefore','LoadAfter',
        'SupportedVersions','DependencyRecords','VersionSpecificDependencies',
        'ForceLoadBefore','ForceLoadAfter','IncompatibleWith','ModVersion','Url','Description'
    )) {
        if ($Snapshot.PSObject.Properties.Name -contains $name) {
            $value = $Snapshot.$name
            if ($Mod.PSObject.Properties.Name -contains $name) { $Mod.$name = $value }
            else { $Mod | Add-Member -NotePropertyName $name -NotePropertyValue $value -Force }
        }
    }
    return $Mod
}

function New-RimForgeAboutSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Mod)
    $snapshot = [ordered]@{}
    foreach ($name in @(
        'Name','PackageId','Author','Dependencies','LoadBefore','LoadAfter',
        'SupportedVersions','DependencyRecords','VersionSpecificDependencies',
        'ForceLoadBefore','ForceLoadAfter','IncompatibleWith','ModVersion','Url','Description'
    )) {
        $snapshot[$name] = if ($Mod.PSObject.Properties.Name -contains $name) { $Mod.$name } else { $null }
    }
    return [PSCustomObject]$snapshot
}

function Import-AboutMetadata {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][array]$Mods,
        [string]$CacheFolder,
        [bool]$UseCache = $false,
        [switch]$PassThruSummary
    )

    $results = @(); $count = @($Mods).Count; $current = 0; $cacheHits = 0; $parsed = 0
    foreach ($mod in @($Mods)) {
        $current++
        if ($count -gt 0) {
            Write-Progress -Activity "Reading About.xml" -Status "$current / $count" -PercentComplete (($current / $count) * 100)
        }

        $cached = $null
        if ($UseCache -and -not [string]::IsNullOrWhiteSpace($CacheFolder)) {
            $key = if (-not [string]::IsNullOrWhiteSpace([string]$mod.WorkshopID)) { 'workshop-' + [string]$mod.WorkshopID } else { 'local-' + [string]$mod.FolderName }
            $entry = Read-RimForgeCacheEntry -CacheRoot $CacheFolder -Namespace 'AboutMetadata' -Key $key -SchemaVersion 1 -SourcePath $mod.AboutPath
            if ($null -ne $entry) { $cached = $entry.Value }
        }

        if ($null -ne $cached) {
            $results += Set-RimForgeAboutSnapshot -Mod $mod -Snapshot $cached
            $cacheHits++
        }
        else {
            $parsedMod = Get-AboutMetadata -Mod $mod
            $results += $parsedMod
            $parsed++
            if ($UseCache -and -not [string]::IsNullOrWhiteSpace($CacheFolder) -and (Test-Path -LiteralPath $mod.AboutPath -PathType Leaf)) {
                $key = if (-not [string]::IsNullOrWhiteSpace([string]$mod.WorkshopID)) { 'workshop-' + [string]$mod.WorkshopID } else { 'local-' + [string]$mod.FolderName }
                Write-RimForgeCacheEntry -CacheRoot $CacheFolder -Namespace 'AboutMetadata' -Key $key -Value (New-RimForgeAboutSnapshot -Mod $parsedMod) -SchemaVersion 1 -SourcePath $mod.AboutPath | Out-Null
            }
        }
    }

    Write-Progress -Activity "Reading About.xml" -Completed
    Write-Log SUCCESS ("About metadata: parsed={0}, cached={1}, total={2}." -f $parsed,$cacheHits,$count)

    if ($PassThruSummary) {
        return [PSCustomObject]@{ Mods=@($results); ParsedCount=$parsed; CacheHitCount=$cacheHits; TotalCount=$count }
    }
    return @($results)
}

Export-ModuleMember `
    -Function Get-AboutMetadata,Set-RimForgeAboutSnapshot,New-RimForgeAboutSnapshot,Import-AboutMetadata