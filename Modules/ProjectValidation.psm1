Set-StrictMode -Version Latest

function Get-RimForgeSafeProperty {
    [CmdletBinding()]
    param(
        [AllowNull()] $InputObject,
        [Parameter(Mandatory)][string]$PropertyName,
        $DefaultValue = $null
    )

    if ($null -eq $InputObject) { return $DefaultValue }
    if ($InputObject.PSObject.Properties.Name -contains $PropertyName) {
        return $InputObject.$PropertyName
    }
    return $DefaultValue
}

function Test-RimForgeConfiguration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $Config,
        [Parameter(Mandatory)][string]$ScriptRoot
    )

    $errors = @()
    $warnings = @()

    foreach ($name in @('Version','RootFolders','OutputFolder','LogFolder','CacheFolder')) {
        if (-not ($Config.PSObject.Properties.Name -contains $name)) {
            $errors += "Config.json is missing required property '$name'."
        }
    }

    if (@($errors).Count -eq 0) {
        if (@($Config.RootFolders).Count -lt 1) {
            $errors += 'Config.RootFolders must contain at least one folder.'
        }

        foreach ($folder in @($Config.RootFolders)) {
            if (-not (Test-Path -LiteralPath ([string]$folder) -PathType Container)) {
                $warnings += "Configured mod root does not currently exist: $folder"
            }
        }

        if ($Config.PSObject.Properties.Name -contains 'ExternalTimeoutSeconds') {
            $timeout = [int]$Config.ExternalTimeoutSeconds
            if ($timeout -lt 1 -or $timeout -gt 300) {
                $errors += 'ExternalTimeoutSeconds must be between 1 and 300.'
            }
        }
    }

    return [PSCustomObject]@{
        IsValid = (@($errors).Count -eq 0)
        Errors = @($errors)
        Warnings = @($warnings)
    }
}

function Test-RimForgeModuleContracts {
    [CmdletBinding()]
    param()

    $requiredCommands = @(
        'Initialize-Logger','Write-Log','Resolve-RimForgeCachePath','Get-RimForgeModFingerprintSet','Compare-RimForgeModState','New-RimForgeTimingSession','Read-RimForgeCacheEntry',
        'Write-RimForgeCacheEntry','Import-RimForgeDependencyManifest','Get-RimForgeDependencyStatus',
        'New-ModRecord','Find-RimWorldMods',
        'Import-AboutMetadata','New-ModIndex','Test-ModLibrary',
        'Import-RimForgeTextureRules','Get-RimForgeTextureInventory',
        'New-RimForgeTextureConversionPlan','Invoke-RimForgeTextureConversion',
        'Test-RimForgeDdsFile','Restore-RimForgeTextures',
        'New-DependencyGraph','Import-EvidenceRules','Invoke-ModEvidenceScan',
        'Find-ModsConfigProfiles','Import-LoadOrderKnowledgeRules',
        'Import-ModTaxonomyDatabase','Test-ModTaxonomyDatabase',
        'Import-LoadOrderBlueprint','Import-BlueprintOverrides',
        'Test-ModVersionStatus','Test-ModsConfigProfile',
        'Import-CompatibilityRules','Test-ProfileCompatibility',
        'Write-CompatibilityReport','Get-ProfileVersionSummary',
        'Get-ProfileTaxonomySummary','Test-ProfileBlueprint',
        'Optimize-ModsConfigLoadOrder','Write-OptimizedModsConfigProfile',
        'Write-ProfileValidationReport','Write-VersionStatusReports',
        'Write-TaxonomyReports','Write-BlueprintReports','Compare-ProfileSet',
        'Write-ProfileSetReports','Write-AuditReport',
        'Export-RimForgeGeneratedDatabase','Test-RimForgeGeneratedDatabase'
    )

    $missing = @(
        foreach ($name in $requiredCommands) {
            if (-not (Get-Command -Name $name -ErrorAction SilentlyContinue)) {
                $name
            }
        }
    )

    return [PSCustomObject]@{
        IsValid = (@($missing).Count -eq 0)
        MissingCommands = @($missing)
    }
}

function Test-RimForgeJsonFiles {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Root)

    $errors = @()
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -Recurse -File -Filter '*.json' -ErrorAction SilentlyContinue)) {
        try {
            Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json | Out-Null
        }
        catch {
            $errors += [PSCustomObject]@{
                Path = $file.FullName
                Error = $_.Exception.Message
            }
        }
    }

    return [PSCustomObject]@{
        IsValid = (@($errors).Count -eq 0)
        Errors = @($errors)
    }
}

Export-ModuleMember -Function Get-RimForgeSafeProperty,Test-RimForgeConfiguration,Test-RimForgeModuleContracts,Test-RimForgeJsonFiles
