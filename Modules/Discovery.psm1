Set-StrictMode -Version Latest

function Find-RimWorldMods {

    param(
        [Parameter(Mandatory)]
        [string[]]$RootFolders
    )

    $mods = @()

    foreach ($root in $RootFolders) {

        if (!(Test-Path $root)) {
            Write-Log WARNING "Root folder not found: $root"
            continue
        }

        Write-Log INFO "Scanning $root"

        Get-ChildItem $root -Directory | ForEach-Object {

            $about = Join-Path $_.FullName "About\About.xml"

            if (Test-Path $about) {

                $mods += New-ModRecord `
                    -RootPath $_.FullName `
                    -FolderName $_.Name

            }

        }

    }

    Write-Log SUCCESS "Discovered $($mods.Count) mods."

    return $mods
}

Export-ModuleMember -Function Find-RimWorldMods