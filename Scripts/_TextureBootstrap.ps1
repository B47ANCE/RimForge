function Initialize-RimForgeTextureContext {
    param([Parameter(Mandatory)][string]$ProjectRoot)

    $moduleFolder = Join-Path $ProjectRoot "Modules"

    foreach ($name in @(
        "Logging.psm1",
        "CacheService.psm1",
        "ModRecord.psm1",
        "Discovery.psm1",
        "AboutParser.psm1",
        "TextureOptimizer.psm1",
        "DependencyManager.psm1"
    )) {
        Import-Module `
            (Join-Path $moduleFolder $name) `
            -Force `
            -DisableNameChecking `
            -ErrorAction Stop
    }

    $config = Get-Content `
        -LiteralPath (Join-Path $ProjectRoot "Config.json") `
        -Raw |
        ConvertFrom-Json

    $logFolder = Join-Path $ProjectRoot $config.LogFolder
    Initialize-Logger -LogDirectory $logFolder

    $mods = Find-RimWorldMods -RootFolders @($config.RootFolders)
    $mods = Import-AboutMetadata -Mods @($mods)

    $rulesPath = Join-Path $ProjectRoot "Database\TextureRules.json"
    $rules = Import-RimForgeTextureRules -Path $rulesPath

    return [PSCustomObject]@{
        Config = $config
        Mods = @($mods)
        Rules = $rules
    }
}

function New-RimForgeConsoleProgressCallback {
    return {
        param($progress)

        Write-Progress `
            -Id 60 `
            -Activity ("RimForge texture {0}" -f $progress.Phase) `
            -Status ("[{0}/{1}] {2}" -f
                $progress.Current,
                $progress.Total,
                $progress.Name) `
            -PercentComplete $progress.Percent
    }
}
