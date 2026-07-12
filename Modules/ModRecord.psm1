Set-StrictMode -Version Latest

function New-ModRecord {
    param(
        [Parameter(Mandatory)]
        [string]$RootPath,

        [Parameter(Mandatory)]
        [string]$FolderName
    )

    $aboutPath = Join-Path $RootPath "About\About.xml"
    $assembliesPath = Join-Path $RootPath "Assemblies"
    $languagesPath = Join-Path $RootPath "Languages"

    $isWorkshop = ($FolderName -match '^\d+$')

    [PSCustomObject]@{
        Name              = $null
        PackageId         = $null
        Author            = $null

        FolderName        = $FolderName
        RootPath          = $RootPath

        IsWorkshop        = $isWorkshop
        WorkshopID        = if ($isWorkshop) { $FolderName } else { $null }

        AboutPath         = $aboutPath
        AssembliesPath    = $assembliesPath
        LanguagesPath     = $languagesPath

        About             = $null

        Assemblies        = @()
        Languages         = @()

        Dependencies      = @()
        OptionalDependencies = @()

        LoadBefore        = @()
        LoadAfter         = @()

        Defs              = @()
        XmlPatches        = @()

        Warnings          = @()
        Errors            = @()
    }
}

Export-ModuleMember -Function New-ModRecord