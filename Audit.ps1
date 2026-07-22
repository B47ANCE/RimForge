# =====================================================================
# RimForge
# Refactored orchestration entry point
# =====================================================================

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $PSCommandPath

# -------------------------------------------------
# Configuration
# -------------------------------------------------

$configPath = Join-Path $ScriptRoot "Config.json"

if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Config.json not found: $configPath"
}

$config = Get-Content `
    -LiteralPath $configPath `
    -Raw |
    ConvertFrom-Json

# -------------------------------------------------
# Modules
# -------------------------------------------------

$moduleFolder = Join-Path $ScriptRoot "Modules"

if (-not (Test-Path -LiteralPath $moduleFolder -PathType Container)) {
    throw "Modules folder not found: $moduleFolder"
}

# Import in dependency order. Avoid wildcard imports because they can hide
# duplicate exports and interface drift between modules.
$moduleOrder = @(
    "ProjectValidation.psm1",
    "RimForgePaths.psm1",
    "Logging.psm1",
    "AuditPipeline.psm1",
    "CacheService.psm1",
    "FingerprintService.psm1",
    "IncrementalAudit.psm1",
    "ModRecord.psm1",
    "Discovery.psm1",
    "AboutParser.psm1",
    "IndexBuilder.psm1",
    "Validator.psm1",
    "DependencyGraph.psm1",
    "EvidenceScanner.psm1",
    "TextureOptimizer.psm1",
    "DependencyManager.psm1",
    "ProfileDiscovery.psm1",
    "ProfileValidation.psm1",
    "ProfileComparison.psm1",
    "CompatibilityScanner.psm1",
    "KnowledgeRules.psm1",
    "TaxonomyManager.psm1",
    "BlueprintScoring.psm1",
    "BlueprintAnalyzer.psm1",
    "LoadOrderOptimizer.psm1",
    "VersionChecker.psm1",
    "DatabaseBuilder.psm1",
    "ReportWriter.psm1"
)

foreach ($moduleName in $moduleOrder) {
    $modulePath = Join-Path $moduleFolder $moduleName
    if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
        throw "Required module not found: $modulePath"
    }
    Import-Module $modulePath -Force -DisableNameChecking -ErrorAction Stop
}

$timingSession = New-RimForgeTimingSession

$contractValidation = Test-RimForgeModuleContracts
if (-not $contractValidation.IsValid) {
    throw (
        "Module contract validation failed. Missing command(s): {0}" -f
        (@($contractValidation.MissingCommands) -join ", ")
    )
}

$jsonValidation = Test-RimForgeJsonFiles -Root $ScriptRoot
if (-not $jsonValidation.IsValid) {
    $messages = @(
        foreach ($item in @($jsonValidation.Errors)) {
            "{0}: {1}" -f $item.Path, $item.Error
        }
    )
    throw ("JSON validation failed:`n" + ($messages -join "`n"))
}

$configValidation = Test-RimForgeConfiguration -Config $config -ScriptRoot $ScriptRoot
if (-not $configValidation.IsValid) {
    throw ("Configuration validation failed:`n" + (@($configValidation.Errors) -join "`n"))
}
foreach ($warning in @($configValidation.Warnings)) {
    Write-Warning $warning
}

# -------------------------------------------------
# Runtime folders and logging
# -------------------------------------------------

$paths = Initialize-RimForgePaths -RepositoryRoot $ScriptRoot -Config $config

$logFolder = $paths.LogsRoot
$outputFolder = $paths.OutputRoot
$profilesFolder = $paths.ProfilesRoot
$optimizedFolder = $paths.OptimizedProfilesRoot
$profileReportsFolder = $paths.ProfileReportsRoot
$cacheFolder = $paths.CacheRoot

Initialize-Logger -LogDirectory $logFolder


# Audit conditions are collected throughout the run so recoverable failures can
# be reported without terminating unrelated stages.
$script:AuditConditions = @()
$script:AuditStages = @()

function Add-RimForgeAuditCondition {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('INFO','WARNING','RECOVERABLE','FATAL')]
        [string]$Severity,

        [Parameter(Mandatory)]
        [string]$Subsystem,

        [Parameter(Mandatory)]
        [string]$Message,

        [AllowNull()]
        [string]$Detail
    )

    $condition = [PSCustomObject]@{
        Timestamp = (Get-Date).ToString('o')
        Severity  = $Severity
        Subsystem = $Subsystem
        Message   = $Message
        Detail    = $Detail
    }

    $script:AuditConditions += $condition

    $display = if ([string]::IsNullOrWhiteSpace($Detail)) {
        "[{0}] {1}" -f $Subsystem, $Message
    }
    else {
        "[{0}] {1} {2}" -f $Subsystem, $Message, $Detail
    }

    Write-Log $Severity $display
    return $condition
}

function New-RimForgeDisabledTaxonomy {
    param([string[]]$UnavailableFiles = @())
    return [PSCustomObject]@{
        SchemaVersion     = 2
        AllowedCategories = @()
        AllowedRoles      = @()
        Entries           = @()
        FamilyRules       = @()
        RolePrecedence    = @()
        TargetVersion     = $targetVersion
        Enabled           = $false
        UnavailableFiles  = @($UnavailableFiles)
    }
}

function New-RimForgeDisabledBlueprint {
    param([string]$SourcePath)
    return [PSCustomObject]@{
        SchemaVersion      = 1
        Name               = 'Blueprint analysis unavailable'
        Description        = 'The curated load-order blueprint could not be loaded.'
        HardRulePrecedence = $true
        TieBreaker         = 'OriginalOrder'
        Sections           = @([PSCustomObject]@{ Id='unclassified'; Name='Unclassified'; Order=9999; MatchScores=[PSCustomObject]@{} })
        SourcePath         = $SourcePath
        Enabled            = $false
    }
}

# Generated report folders are replaceable, but persistent profiles, caches,
# user data, and databases under Output must never be deleted by an audit.
foreach ($folder in @($optimizedFolder, $profileReportsFolder, $paths.ReportsRoot)) {
    if (Test-Path -LiteralPath $folder -PathType Container) {
        Get-ChildItem -LiteralPath $folder -Force -ErrorAction SilentlyContinue |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
}

$compatibilityRulesPath = Resolve-RimForgeCuratedAsset -Paths $paths -FileName 'CompatibilityRules.json'

try {
    $compatibilityRules = Import-CompatibilityRules `
        -Path $compatibilityRulesPath
}
catch {
    Add-RimForgeAuditCondition `
        -Severity RECOVERABLE `
        -Subsystem 'Compatibility' `
        -Message 'Curated compatibility rules could not be loaded. Continuing without them.' `
        -Detail $_.Exception.Message | Out-Null
    $compatibilityRules = [PSCustomObject]@{ SchemaVersion = 1; Rules = @() }
}

Write-Log INFO (
    "Loaded {0} custom compatibility rule(s)." -f
    @($compatibilityRules.Rules).Count
)

if (-not (Test-Path -LiteralPath $compatibilityRulesPath -PathType Leaf)) {
    Add-RimForgeAuditCondition `
        -Severity WARNING `
        -Subsystem 'Compatibility' `
        -Message 'No curated compatibility rules were found. Declared incompatibilities will still be checked.' `
        -Detail $compatibilityRulesPath | Out-Null
}

Write-Log SUCCESS "=============================================="
Write-Log SUCCESS " RimForge v$($config.Version)"
Write-Log INFO " Forging stable, optimized modpack ecosystems."
Write-Log SUCCESS "=============================================="
Write-Log INFO "Configuration loaded."
Write-Log INFO "Modules loaded."

# -------------------------------------------------
# Installed mod library
# -------------------------------------------------

Start-RimForgeTimingStage -Session $timingSession -Name 'Discovery'
$discoveryRoots = @(Get-RimForgeDiscoveryRoots -Config $config -Paths $paths)
if ($discoveryRoots.Count -lt 1) { Write-Log WARNING 'No valid mod roots were discovered. Continuing with an empty library.' }
$mods = Find-RimWorldMods -RootFolders $discoveryRoots
[void](Stop-RimForgeTimingStage -Session $timingSession -Name 'Discovery')

Write-Log INFO "Discovery complete."
Write-Log INFO "Found $(@($mods).Count) RimWorld mods."

$incrementalEnabled = $true
if ($config.PSObject.Properties.Name -contains 'Incremental' -and $config.Incremental.PSObject.Properties.Name -contains 'Enabled') {
    $incrementalEnabled = [bool]$config.Incremental.Enabled
}
$incrementalStatePath = Join-Path $cacheFolder 'Incremental\ModState.json'
$previousIncrementalState = if ($incrementalEnabled) { Read-RimForgeIncrementalState -Path $incrementalStatePath } else { $null }

Start-RimForgeTimingStage -Session $timingSession -Name 'Fingerprinting'
$fullVerificationIntervalDays = 7
if (
    $config.PSObject.Properties.Name -contains 'Incremental' -and
    $config.Incremental.PSObject.Properties.Name -contains 'FullVerificationIntervalDays'
) {
    $fullVerificationIntervalDays = [Math]::Max(1, [int]$config.Incremental.FullVerificationIntervalDays)
}

$modFingerprints = Get-RimForgeModFingerprintSet `
    -Mods @($mods) `
    -PreviousState $previousIncrementalState `
    -FullVerificationIntervalDays $fullVerificationIntervalDays `
    -DisableProgress:(-not [bool]$config.Performance.ShowProgress)
$incrementalComparison = Compare-RimForgeModState -CurrentFingerprints @($modFingerprints) -PreviousState $previousIncrementalState
[void](Stop-RimForgeTimingStage -Session $timingSession -Name 'Fingerprinting')

Write-Log INFO ("Incremental state: total={0}, changed={1}, unchanged={2}, added={3}, removed={4}" -f $incrementalComparison.Total,$incrementalComparison.ChangedCount,$incrementalComparison.UnchangedCount,$incrementalComparison.AddedCount,$incrementalComparison.RemovedCount)

Start-RimForgeTimingStage -Session $timingSession -Name 'AboutMetadata'
$aboutImport = Import-AboutMetadata -Mods @($mods) -CacheFolder $cacheFolder -UseCache:$incrementalEnabled -PassThruSummary
$mods = @($aboutImport.Mods)
[void](Stop-RimForgeTimingStage -Session $timingSession -Name 'AboutMetadata')

$index = New-ModIndex -Mods @($mods)

$validation = Test-ModLibrary `
    -Mods @($mods) `
    -Index $index

$dependencyGraph = New-DependencyGraph `
    -Mods @($mods) `
    -Index $index

# Resolve the target version before evidence scanning so only the active
# RimWorld-version folders are analyzed.
$targetVersion = "1.6"

if (
    $config.PSObject.Properties.Name -contains "TargetRimWorldVersion" -and
    -not [string]::IsNullOrWhiteSpace(
        [string]$config.TargetRimWorldVersion
    )
) {
    $targetVersion = [string]$config.TargetRimWorldVersion
}

# -------------------------------------------------
# Objective file evidence scan
# -------------------------------------------------

$evidenceRulesPath = Resolve-RimForgeCuratedAsset -Paths $paths -FileName 'EvidenceRules.json'
try {
    $evidenceRules = Import-EvidenceRules -Path $evidenceRulesPath -AllowMissing
}
catch {
    Add-RimForgeAuditCondition `
        -Severity RECOVERABLE `
        -Subsystem 'Evidence' `
        -Message 'PowerShell Evidence rules are invalid. Native Evidence and the remaining audit will continue.' `
        -Detail $_.Exception.Message | Out-Null
    $evidenceRules = [PSCustomObject]@{
        Enabled = $false; Categories = @(); SourcePath = $evidenceRulesPath
        UnavailableReason = $_.Exception.Message
    }
}

if ($evidenceRules.Enabled) {
    Write-Log INFO "Scanning mod XML, paths, and assembly metadata for objective evidence."
}
else {
    Add-RimForgeAuditCondition `
        -Severity RECOVERABLE `
        -Subsystem 'Evidence' `
        -Message 'Curated Evidence rules are unavailable. Continuing with native Evidence and the remaining audit.' `
        -Detail ([string]$evidenceRules.UnavailableReason) | Out-Null
}

$evidenceCacheFolder = Join-Path $cacheFolder "Evidence"
$evidenceReportsFolder = $paths.EvidenceReportsRoot
$useEvidenceCache = $true

if (
    $config.PSObject.Properties.Name -contains "Performance" -and
    $config.Performance.PSObject.Properties.Name -contains "UseCache"
) {
    $useEvidenceCache = [bool]$config.Performance.UseCache
}

$unchangedKeys = @{}
foreach ($item in @($incrementalComparison.Unchanged)) { $unchangedKeys[[string]$item.Key] = $true }
$trustedEvidencePackageIds = @(
    foreach ($mod in @($mods)) {
        $key = if (-not [string]::IsNullOrWhiteSpace([string]$mod.WorkshopID)) { 'workshop-' + [string]$mod.WorkshopID } else { 'local-' + [string]$mod.FolderName }
        if ($unchangedKeys.ContainsKey($key) -and -not [string]::IsNullOrWhiteSpace([string]$mod.PackageId)) {
            [string]$mod.PackageId
        }
    }
)

Start-RimForgeTimingStage -Session $timingSession -Name 'EvidenceScan'
if ($evidenceRules.Enabled) {
    $evidenceScan = Invoke-ModEvidenceScan `
        -Mods @($mods) `
        -Rules $evidenceRules `
        -OutputFolder $evidenceReportsFolder `
        -ProgressId 20 `
        -CacheFolder $evidenceCacheFolder `
        -UseCache $useEvidenceCache `
        -TrustedUnchangedPackageIds @($trustedEvidencePackageIds) `
        -TargetVersion $targetVersion
}
else {
    $evidenceScan = [PSCustomObject]@{
        ModCount = @($mods).Count
        ClassifiedCount = 0
        ReviewCount = 0
        CacheHitCount = 0
        ScannedCount = 0
        CacheWriteErrorCount = 0
        CacheFolder = $evidenceCacheFolder
        ReportPath = $null
        ReviewPath = $null
        Results = @()
        Disabled = $true
    }
}
[void](Stop-RimForgeTimingStage -Session $timingSession -Name 'EvidenceScan')

Write-Log INFO (
    "Evidence scan: mods={0}, classified={1}, review queue={2}, cached={3}, scanned={4}" -f
    $evidenceScan.ModCount,
    $evidenceScan.ClassifiedCount,
    $evidenceScan.ReviewCount,
    $evidenceScan.CacheHitCount,
    $evidenceScan.ScannedCount
)

if ($evidenceScan.CacheWriteErrorCount -gt 0) {
    Write-Log WARNING (
        "Evidence cache write failures: {0}" -f
        $evidenceScan.CacheWriteErrorCount
    )
}

if ($dependencyGraph.MissingDependencyCount -gt 0) {
    Write-Log WARNING "Missing dependency details:"

    foreach ($item in @($dependencyGraph.MissingDependencies)) {
        Write-Log WARNING (
            "{0} [{1}] requires missing package {2}" -f
            $item.RequiredByName,
            $item.RequiredByPackageId,
            $item.MissingPackageId
        )
    }
}

if ($dependencyGraph.CycleCount -gt 0) {
    Write-Log WARNING "Dependency cycle details:"

    foreach ($cycle in @($dependencyGraph.Cycles)) {
        Write-Log WARNING (
            @($cycle.PackageIds) -join " -> "
        )
    }
}

# -------------------------------------------------
# Discover all profiles
# -------------------------------------------------

$profiles = @(Find-ModsConfigProfiles `
    -ProfilesFolder $profilesFolder)

$profileAnalysisMode = "RimForgeProfiles"
$profileSourceDescription = $profilesFolder

if (@($profiles).Count -lt 1) {
    $localApplicationData = [Environment]::GetFolderPath(
        [Environment+SpecialFolder]::LocalApplicationData
    )
    $localLow = Join-Path (Split-Path -Parent $localApplicationData) "LocalLow"
    $liveModsConfigPath = Join-Path `
        $localLow `
        "Ludeon Studios\RimWorld by Ludeon Studios\Config\ModsConfig.xml"

    if (Test-Path -LiteralPath $liveModsConfigPath -PathType Leaf) {
        try {
            $liveProfile = Import-ModsConfigProfile `
                -Path $liveModsConfigPath `
                -Name "Current RimWorld Profile"

            $hasCore = @($liveProfile.NormalizedIds) -contains "ludeon.rimworld"

            if ($liveProfile.Count -gt 0 -and $hasCore) {
                $profiles = @($liveProfile)
                $profileAnalysisMode = "LiveModsConfig"
                $profileSourceDescription = $liveModsConfigPath

                Write-Log WARNING (
                    "No RimForge profile was available. Using RimWorld's current ModsConfig.xml instead: {0}" -f
                    $liveModsConfigPath
                )
            }
        }
        catch {
            Write-Log WARNING (
                "RimWorld's current ModsConfig.xml could not be loaded: {0}" -f
                $_.Exception.Message
            )
        }
    }
}

if (@($profiles).Count -lt 1) {
    $profiles = @()
    $profileAnalysisMode = "LibraryOnly"
    $profileSourceDescription = $null

    Add-RimForgeAuditCondition `
        -Severity RECOVERABLE `
        -Subsystem 'Profiles' `
        -Message 'No active profile was found. Continuing with library-wide analysis.' `
        -Detail 'Profile validation, compatibility, optimization, and comparison were skipped.' | Out-Null
}
else {
    Write-Log SUCCESS (
        "Discovered {0} valid profile(s)." -f
        @($profiles).Count
    )
}

$loadOrderRulesPath = Resolve-RimForgeCuratedAsset -Paths $paths -FileName 'LoadOrderRules.json'

try {
    $loadOrderKnowledge = Import-LoadOrderKnowledgeRules `
        -Path $loadOrderRulesPath `
        -TargetVersion $targetVersion
}
catch {
    Add-RimForgeAuditCondition `
        -Severity RECOVERABLE `
        -Subsystem 'LoadOrder' `
        -Message 'Curated load-order rules could not be loaded. Optimization will use declared dependencies only.' `
        -Detail $_.Exception.Message | Out-Null
    $loadOrderKnowledge = [PSCustomObject]@{ SchemaVersion = 1; Rules = @(); SourcePath = $loadOrderRulesPath }
}

Write-Log INFO (
    "Loaded {0} curated load-order rule(s)." -f
    @($loadOrderKnowledge.Rules).Count
)

if (-not (Test-Path -LiteralPath $loadOrderRulesPath -PathType Leaf)) {
    Add-RimForgeAuditCondition `
        -Severity WARNING `
        -Subsystem 'LoadOrder' `
        -Message 'No curated load-order rules were found. Declared dependencies will remain authoritative.' `
        -Detail $loadOrderRulesPath | Out-Null
}

$taxonomyPath = Resolve-RimForgeCuratedAsset -Paths $paths -FileName 'ModTaxonomy.json'

$taxonomyRulesPath = Resolve-RimForgeCuratedAsset -Paths $paths -FileName 'TaxonomyRules.json'

$familyRulesPath = Resolve-RimForgeCuratedAsset -Paths $paths -FileName 'FamilyRules.json'

try {
    $modTaxonomy = Import-ModTaxonomyDatabase `
        -TaxonomyPath $taxonomyPath `
        -RulesPath $taxonomyRulesPath `
        -FamilyRulesPath $familyRulesPath `
        -TargetVersion $targetVersion

    $taxonomyValidation = Test-ModTaxonomyDatabase `
        -Database $modTaxonomy `
        -InstalledMods @($mods)

    Write-Log INFO (
        "Taxonomy database: explicit={0}, family rules={1}, errors={2}, warnings={3}" -f
        $taxonomyValidation.ExplicitEntryCount,
        $taxonomyValidation.FamilyRuleCount,
        $taxonomyValidation.ErrorCount,
        $taxonomyValidation.WarningCount
    )

    if (-not $taxonomyValidation.IsValid) {
        Add-RimForgeAuditCondition `
            -Severity RECOVERABLE `
            -Subsystem 'Taxonomy' `
            -Message 'The curated taxonomy contains validation errors. Taxonomy and blueprint analysis were disabled for this run.' `
            -Detail ("{0} validation error(s)." -f $taxonomyValidation.ErrorCount) | Out-Null
        $modTaxonomy = New-RimForgeDisabledTaxonomy
    }
}
catch {
    Add-RimForgeAuditCondition `
        -Severity RECOVERABLE `
        -Subsystem 'Taxonomy' `
        -Message 'The curated taxonomy could not be loaded. Taxonomy and blueprint analysis were disabled for this run.' `
        -Detail $_.Exception.Message | Out-Null
    $modTaxonomy = New-RimForgeDisabledTaxonomy -UnavailableFiles @($taxonomyPath,$taxonomyRulesPath,$familyRulesPath)
    $taxonomyValidation = [PSCustomObject]@{
        ExplicitEntryCount = 0; FamilyRuleCount = 0; ErrorCount = 0; WarningCount = 0
        Errors = @(); Warnings = @(); IsValid = $true; Disabled = $true
    }
}

if ($modTaxonomy.PSObject.Properties.Name -contains 'Enabled' -and -not [bool]$modTaxonomy.Enabled) {
    Add-RimForgeAuditCondition `
        -Severity RECOVERABLE `
        -Subsystem 'Taxonomy' `
        -Message 'Curated taxonomy data is unavailable. Taxonomy and blueprint classification will be limited.' `
        -Detail ((@($modTaxonomy.UnavailableFiles) -join '; ')) | Out-Null
}

$blueprintPath = Resolve-RimForgeCuratedAsset -Paths $paths -FileName 'LoadOrderBlueprint.json'

try {
    $loadOrderBlueprint = Import-LoadOrderBlueprint `
        -Path $blueprintPath
}
catch {
    Add-RimForgeAuditCondition `
        -Severity RECOVERABLE `
        -Subsystem 'Blueprint' `
        -Message 'The curated load-order blueprint could not be loaded. Blueprint scoring was disabled.' `
        -Detail $_.Exception.Message | Out-Null
    $loadOrderBlueprint = New-RimForgeDisabledBlueprint -SourcePath $blueprintPath
}

if ($loadOrderBlueprint.PSObject.Properties.Name -contains 'Enabled' -and -not [bool]$loadOrderBlueprint.Enabled) {
    Add-RimForgeAuditCondition `
        -Severity RECOVERABLE `
        -Subsystem 'Blueprint' `
        -Message 'Curated blueprint data is unavailable. Blueprint scoring will be skipped.' `
        -Detail $blueprintPath | Out-Null
}

$blueprintOverridesPath = Resolve-RimForgeCuratedAsset -Paths $paths -FileName 'BlueprintOverrides.json'

try {
    $blueprintOverrides = Import-BlueprintOverrides `
        -Path $blueprintOverridesPath
}
catch {
    Add-RimForgeAuditCondition `
        -Severity RECOVERABLE `
        -Subsystem 'Blueprint' `
        -Message 'Blueprint overrides could not be loaded. Continuing without overrides.' `
        -Detail $_.Exception.Message | Out-Null
    $blueprintOverrides = [PSCustomObject]@{ SchemaVersion = 1; Overrides = @(); SourcePath = $blueprintOverridesPath }
}

Write-Log INFO (
    "Loaded load-order blueprint with {0} section(s) and {1} override(s)." -f
    @($loadOrderBlueprint.Sections).Count,
    @($blueprintOverrides.Overrides).Count
)

$externalTimeoutSeconds = 10

if ($config.PSObject.Properties.Name -contains "ExternalTimeoutSeconds") {
    $configuredTimeout = [int]$config.ExternalTimeoutSeconds

    if ($configuredTimeout -ge 1 -and $configuredTimeout -le 300) {
        $externalTimeoutSeconds = $configuredTimeout
    }
}

Write-Log INFO (
    "Checking RimWorld {0} support and Steam Workshop status (external timeout: {1}s)." -f
    $targetVersion,
    $externalTimeoutSeconds
)

try {
    $versionStatus = Test-ModVersionStatus `
        -Mods @($mods) `
        -TargetVersion $targetVersion `
        -CacheFolder $cacheFolder `
        -CacheHours 24 `
        -ExternalTimeoutSeconds $externalTimeoutSeconds
}
catch {
    Add-RimForgeAuditCondition `
        -Severity RECOVERABLE `
        -Subsystem 'VersionStatus' `
        -Message 'Version and Workshop status checks could not be completed. Continuing without version enrichment.' `
        -Detail $_.Exception.Message | Out-Null
    $versionStatus = [PSCustomObject]@{
        Mods = @(); NativeSupportCount = 0; NoVersionWarningCount = 0
        UnsupportedUnknownCount = 0; WorkshopUnavailableCount = 0
        PossiblyStaleCount = 0; ExternalChecksSkipped = $true; Disabled = $true
    }
}

if ($versionStatus.ExternalChecksSkipped) {
    Add-RimForgeAuditCondition `
        -Severity WARNING `
        -Subsystem 'VersionStatus' `
        -Message 'One or more external checks timed out or were unavailable.' `
        -Detail 'Cached data was used when available; remaining online checks were skipped.' | Out-Null
}

Write-Log INFO (
    "Version status: native={0}, NoVersionWarning={1}, unknown={2}, Workshop unavailable={3}, possibly stale={4}" -f
    $versionStatus.NativeSupportCount,
    $versionStatus.NoVersionWarningCount,
    $versionStatus.UnsupportedUnknownCount,
    $versionStatus.WorkshopUnavailableCount,
    $versionStatus.PossiblyStaleCount
)

# -------------------------------------------------
# Validate and optimize every profile
# -------------------------------------------------

$profileResults = @()

foreach ($profile in @($profiles)) {
    try {
    Write-Log INFO (
        "Processing profile '{0}' ({1} mods)." -f
        $profile.Name,
        $profile.Count
    )

    $profileValidation = Test-ModsConfigProfile `
        -Profile $profile `
        -Mods @($mods)

    $compatibility = Test-ProfileCompatibility `
        -Profile $profile `
        -Mods @($mods) `
        -Rules $compatibilityRules

    $compatibilityReport = Write-CompatibilityReport `
        -CompatibilityResult $compatibility `
        -OutputFolder $profileReportsFolder

    $profileVersionSummary = Get-ProfileVersionSummary `
        -Profile $profile `
        -VersionStatus $versionStatus

    $profileTaxonomySummary = Get-ProfileTaxonomySummary `
        -Profile $profile `
        -Database $modTaxonomy

    $profileBlueprintResult = Test-ProfileBlueprint `
        -ProfileTaxonomySummary $profileTaxonomySummary `
        -Blueprint $loadOrderBlueprint `
        -Overrides $blueprintOverrides

    $optimization = Optimize-ModsConfigLoadOrder `
        -Profile $profile `
        -Mods @($mods) `
        -KnowledgeRules $loadOrderKnowledge

    $optimizedFiles = Write-OptimizedModsConfigProfile `
        -Profile $profile `
        -Optimization $optimization `
        -OutputFolder $optimizedFolder

    $profileResult = [PSCustomObject]@{
        Profile      = $profile
        Validation    = $profileValidation
        Compatibility = $compatibility
        VersionStatus = $profileVersionSummary
        Taxonomy      = $profileTaxonomySummary
        Blueprint      = $profileBlueprintResult
        Optimization  = $optimization
        OutputFiles   = [PSCustomObject]@{
            OptimizedProfile   = $optimizedFiles.ProfilePath
            OptimizationReport = $optimizedFiles.ReportPath
            CompatibilityReport = $compatibilityReport
        }
    }

    $profileResults += $profileResult

    Write-ProfileValidationReport `
        -ProfileResult $profileResult `
        -OutputFolder $profileReportsFolder |
        Out-Null

    Write-Log INFO (
        "{0}: missing installed={1}, missing deps={2}, missing DLC={3}, load issues={4}, incompatibilities={5}, suggested moves={6}" -f
        $profile.Name,
        @($profileValidation.MissingInstalledMods).Count,
        @($profileValidation.MissingDependencies).Count,
        @($profileValidation.MissingDlc).Count,
        @($profileValidation.LoadOrderIssues).Count,
        $compatibility.TotalCount,
        $optimization.MoveCount
    )

    }
    catch {
        Add-RimForgeAuditCondition `
            -Severity RECOVERABLE `
            -Subsystem 'Profiles' `
            -Message ("Profile '{0}' could not be fully analyzed; remaining profiles will continue." -f $profile.Name) `
            -Detail $_.Exception.Message | Out-Null
    }
}

$profileVersionSummaries = @(
    foreach ($result in @($profileResults)) {
        $result.VersionStatus
    }
)

$versionReportStage = Invoke-RimForgeAuditStage `
    -Name 'Write version-status reports' `
    -Subsystem 'VersionStatus' `
    -FailureSeverity RECOVERABLE `
    -FailureMessage 'Version-status reports could not be written. Continuing with the remaining audit outputs.' `
    -FallbackValue $null `
    -OnFailure {
        param($stageError)
        Add-RimForgeAuditCondition `
            -Severity RECOVERABLE `
            -Subsystem 'VersionStatus' `
            -Message 'Version-status reports could not be written. Continuing with the remaining audit outputs.' `
            -Detail $stageError.Exception.Message | Out-Null
    } `
    -Action {
        Write-VersionStatusReports `
            -VersionStatus $versionStatus `
            -ProfileSummaries @($profileVersionSummaries) `
            -OutputFolder $paths.ReportsRoot
    }
$script:AuditStages += $versionReportStage

$profileTaxonomySummaries = @(
    foreach ($result in @($profileResults)) {
        $result.Taxonomy
    }
)

$taxonomyReportStage = Invoke-RimForgeAuditStage `
    -Name 'Write taxonomy reports' `
    -Subsystem 'Taxonomy' `
    -FailureSeverity RECOVERABLE `
    -FailureMessage 'Taxonomy reports could not be written. Continuing with the remaining audit outputs.' `
    -FallbackValue $null `
    -OnFailure {
        param($stageError)
        Add-RimForgeAuditCondition `
            -Severity RECOVERABLE `
            -Subsystem 'Taxonomy' `
            -Message 'Taxonomy reports could not be written. Continuing with the remaining audit outputs.' `
            -Detail $stageError.Exception.Message | Out-Null
    } `
    -Action {
        Write-TaxonomyReports `
            -Validation $taxonomyValidation `
            -ProfileSummaries @($profileTaxonomySummaries) `
            -OutputFolder $paths.ReportsRoot
    }
$script:AuditStages += $taxonomyReportStage


$profileBlueprintResults = @(
    foreach ($result in @($profileResults)) {
        $result.Blueprint
    }
)

$blueprintReportStage = Invoke-RimForgeAuditStage `
    -Name 'Write blueprint reports' `
    -Subsystem 'Blueprint' `
    -FailureSeverity RECOVERABLE `
    -FailureMessage 'Blueprint reports could not be written. Continuing with the remaining audit outputs.' `
    -FallbackValue $null `
    -OnFailure {
        param($stageError)
        Add-RimForgeAuditCondition `
            -Severity RECOVERABLE `
            -Subsystem 'Blueprint' `
            -Message 'Blueprint reports could not be written. Continuing with the remaining audit outputs.' `
            -Detail $stageError.Exception.Message | Out-Null
    } `
    -Action {
        Write-BlueprintReports `
            -Blueprint $loadOrderBlueprint `
            -ProfileBlueprintResults @($profileBlueprintResults) `
            -OutputFolder $paths.ReportsRoot
    }
$script:AuditStages += $blueprintReportStage


# -------------------------------------------------
# Build persistent GitHub-ready generated database
# -------------------------------------------------

$databaseBuilderEnabled = $true
$generatedDatabaseFolder = $paths.GeneratedDatabaseRoot

if ($config.PSObject.Properties.Name -contains "DatabaseBuilder" -and
    $config.DatabaseBuilder.PSObject.Properties.Name -contains "Enabled") {
    $databaseBuilderEnabled = [bool]$config.DatabaseBuilder.Enabled
}

$databaseBuild = $null

if ($databaseBuilderEnabled) {
    Write-Log INFO (
        "Building generated mod database at {0}" -f
        $generatedDatabaseFolder
    )

    try {
        $databaseBuild = Export-RimForgeGeneratedDatabase `
            -Mods @($mods) `
            -EvidenceScan $evidenceScan `
            -VersionStatus $versionStatus `
            -TaxonomyDatabase $modTaxonomy `
            -DatabaseRoot $generatedDatabaseFolder `
            -TargetVersion $targetVersion `
            -ScannerVersion ([string]$config.Version) `
            -ProgressId 40

        $databaseValidation = Test-RimForgeGeneratedDatabase `
            -DatabaseRoot $generatedDatabaseFolder

        if (-not $databaseValidation.IsValid) {
            Add-RimForgeAuditCondition `
                -Severity RECOVERABLE `
                -Subsystem 'GeneratedDatabase' `
                -Message 'Generated database validation failed. Audit reports remain available, but the generated database was not accepted.' `
                -Detail ((@($databaseValidation.Errors) -join ' ')) | Out-Null
            $databaseBuild = $null
        }
        else {
            Write-Log SUCCESS (
                "Generated database: records={0}, written={1}, unchanged={2}, review={3}, quarantine={4}" -f
                $databaseBuild.RecordCount,
                $databaseBuild.WrittenCount,
                $databaseBuild.UnchangedCount,
                $databaseBuild.ReviewCount,
                $databaseBuild.QuarantineCount
            )
        }
    }
    catch {
        Add-RimForgeAuditCondition `
            -Severity RECOVERABLE `
            -Subsystem 'GeneratedDatabase' `
            -Message 'Generated database construction failed. Core audit reports remain available.' `
            -Detail $_.Exception.Message | Out-Null
        $databaseBuild = $null
    }
}
else {
    Write-Log INFO "Generated database builder is disabled."
}

# -------------------------------------------------
# Compare the full profile set when more than one exists
# -------------------------------------------------

$profileSetComparison = $null

if (@($profiles).Count -gt 1) {
    $profileComparisonStage = Invoke-RimForgeAuditStage `
        -Name 'Compare profile set' `
        -Subsystem 'Profiles' `
        -FailureSeverity RECOVERABLE `
        -FailureMessage 'Cross-profile comparison could not be completed. Continuing with global reports.' `
        -FallbackValue $null `
        -OnFailure {
            param($stageError)
            Add-RimForgeAuditCondition `
                -Severity RECOVERABLE `
                -Subsystem 'Profiles' `
                -Message 'Cross-profile comparison could not be completed. Continuing with global reports.' `
                -Detail $stageError.Exception.Message | Out-Null
        } `
        -Action {
            $comparison = Compare-ProfileSet -Profiles @($profiles)
            Write-ProfileSetReports `
                -Comparison $comparison `
                -ProfileResults @($profileResults) `
                -OutputFolder $paths.ReportsRoot | Out-Null
            $comparison
        }
    $script:AuditStages += $profileComparisonStage
    $profileSetComparison = $profileComparisonStage.Value

    if ($null -ne $profileSetComparison) {
        Write-Log SUCCESS "Profile-set comparison complete."
        Write-Log INFO (
            "Mods shared across every profile: {0}" -f
            $profileSetComparison.SharedAcrossAllCount
        )

        foreach ($unique in @($profileSetComparison.UniqueByProfile)) {
            Write-Log INFO (
                "{0} unique mods: {1}" -f
                $unique.ProfileName,
                $unique.Count
            )
        }
    }
}
elseif (@($profiles).Count -eq 1) {
    Write-Log INFO "Cross-profile comparison skipped: only one profile is available."
}
else {
    Write-Log INFO "Cross-profile comparison skipped: no profile set is available in library analysis mode."
}

# -------------------------------------------------
# Global audit report
# -------------------------------------------------

$globalReportStage = Invoke-RimForgeAuditStage `
    -Name 'Write global audit report' `
    -Subsystem 'Reports' `
    -FailureSeverity RECOVERABLE `
    -FailureMessage 'The global audit report could not be written. Other completed reports remain available.' `
    -FallbackValue $null `
    -OnFailure {
        param($stageError)
        Add-RimForgeAuditCondition `
            -Severity RECOVERABLE `
            -Subsystem 'Reports' `
            -Message 'The global audit report could not be written. Other completed reports remain available.' `
            -Detail $stageError.Exception.Message | Out-Null
    } `
    -Action {
        Write-AuditReport `
            -Mods @($mods) `
            -Validation $validation `
            -DependencyGraph $dependencyGraph `
            -OutputFolder $paths.ReportsRoot `
            -Version $config.Version
    }
$script:AuditStages += $globalReportStage

# Optional combined summary for the refactored pipeline.
$pipelineSummary = [PSCustomObject]@{
    Generated            = (Get-Date).ToString("o")
    ProfileCount         = @($profiles).Count
    ProfileAnalysisMode  = $profileAnalysisMode
    ProfileSource        = $profileSourceDescription
    Profiles             = @($profileResults)
    ProfileSetComparison = $profileSetComparison
    EvidenceScan         = [PSCustomObject]@{
        ModCount        = $evidenceScan.ModCount
        ClassifiedCount = $evidenceScan.ClassifiedCount
        ReviewCount     = $evidenceScan.ReviewCount
        CacheHitCount   = $evidenceScan.CacheHitCount
        ScannedCount    = $evidenceScan.ScannedCount
        CacheFolder     = $evidenceScan.CacheFolder
        ReportPath      = $evidenceScan.ReportPath
        ReviewPath      = $evidenceScan.ReviewPath
    }
    DatabaseBuild        = $databaseBuild
    Conditions           = @($script:AuditConditions)
    Stages               = @($script:AuditStages)
    ConditionSummary     = [PSCustomObject]@{
        WarningCount     = @($script:AuditConditions | Where-Object Severity -eq 'WARNING').Count
        RecoverableCount = @($script:AuditConditions | Where-Object Severity -eq 'RECOVERABLE').Count
        FatalCount       = @($script:AuditConditions | Where-Object Severity -eq 'FATAL').Count
    }
}

$timingSummary = Complete-RimForgeTimingSession -Session $timingSession
$incrementalRunSummary = [PSCustomObject]@{
    Version = [string]$config.Version
    TotalMods = $incrementalComparison.Total
    ChangedMods = $incrementalComparison.ChangedCount
    UnchangedMods = $incrementalComparison.UnchangedCount
    AddedMods = $incrementalComparison.AddedCount
    RemovedMods = $incrementalComparison.RemovedCount
    AboutParsed = $aboutImport.ParsedCount
    AboutCached = $aboutImport.CacheHitCount
    EvidenceScanned = $evidenceScan.ScannedCount
    EvidenceCached = $evidenceScan.CacheHitCount
    Timing = $timingSummary
}

if ($incrementalEnabled) {
    Write-RimForgeIncrementalState -Path $incrementalStatePath -Fingerprints @($modFingerprints) -RunSummary $incrementalRunSummary | Out-Null
}

$pipelineSummary | Add-Member -NotePropertyName Incremental -NotePropertyValue $incrementalRunSummary -Force
$pipelineSummary |
    ConvertTo-Json -Depth 20 |
    Set-Content `
        -LiteralPath (Join-Path $paths.ReportsRoot "ProfilePipeline.json") `
        -Encoding UTF8

[PSCustomObject]@{
    Generated = (Get-Date).ToString('o')
    Summary = $pipelineSummary.ConditionSummary
    Conditions = @($script:AuditConditions)
} |
    ConvertTo-Json -Depth 10 |
    Set-Content `
        -LiteralPath (Join-Path $paths.ReportsRoot 'AuditConditions.json') `
        -Encoding UTF8

[PSCustomObject]@{
    Generated = (Get-Date).ToString('o')
    Stages = @($script:AuditStages | ForEach-Object {
        [PSCustomObject]@{
            Name = $_.Name
            Subsystem = $_.Subsystem
            Status = $_.Status
            Started = $_.Started
            Completed = $_.Completed
            ElapsedMilliseconds = $_.ElapsedMilliseconds
            Error = $_.Error
        }
    })
} |
    ConvertTo-Json -Depth 8 |
    Set-Content `
        -LiteralPath (Join-Path $paths.ReportsRoot 'AuditStages.json') `
        -Encoding UTF8

$incrementalRunSummary |
    ConvertTo-Json -Depth 20 |
    Set-Content -LiteralPath (Join-Path $paths.ReportsRoot 'IncrementalAudit.json') -Encoding UTF8

Write-Log INFO ("Incremental cache: {0} About.xml record(s) reused; {1} Evidence result(s) reused." -f $aboutImport.CacheHitCount,$evidenceScan.CacheHitCount)
Write-Log INFO ("Forge timing: {0:N2}s total (discovery {1:N2}s, fingerprinting {2:N2}s, metadata {3:N2}s, Evidence {4:N2}s)." -f ($timingSummary.TotalMilliseconds/1000),($timingSummary.Stages.Discovery/1000),($timingSummary.Stages.Fingerprinting/1000),($timingSummary.Stages.AboutMetadata/1000),($timingSummary.Stages.EvidenceScan/1000))
$recoverableConditions = @($script:AuditConditions | Where-Object Severity -eq 'RECOVERABLE')
$warningConditions = @($script:AuditConditions | Where-Object Severity -eq 'WARNING')
$fatalConditions = @($script:AuditConditions | Where-Object Severity -eq 'FATAL')
$recoverableCount = $recoverableConditions.Count
$warningConditionCount = $warningConditions.Count
$fatalConditionCount = $fatalConditions.Count
$completedStageCount = @($script:AuditStages | Where-Object Status -eq 'Completed').Count
$nonCompletedStageCount = @($script:AuditStages | Where-Object Status -ne 'Completed').Count
$totalSeconds = [math]::Round($timingSummary.TotalMilliseconds / 1000, 2)

$forgeResult = if ($fatalConditionCount -gt 0) {
    'Failed'
}
elseif (($recoverableCount + $warningConditionCount) -gt 0) {
    'CompletedWithConditions'
}
else {
    'Completed'
}

$forgeSummary = [PSCustomObject]@{
    Generated = (Get-Date).ToString('o')
    Result = $forgeResult
    AnalysisMode = $profileAnalysisMode
    ModsAnalyzed = @($mods).Count
    ProfilesAnalyzed = @($profileResults).Count
    DurationSeconds = $totalSeconds
    CompletedStages = $completedStageCount
    NonCompletedStages = $nonCompletedStageCount
    WarningCount = $warningConditionCount
    RecoverableCount = $recoverableCount
    FatalCount = $fatalConditionCount
    Conditions = @($script:AuditConditions | ForEach-Object {
        [PSCustomObject]@{
            Severity = $_.Severity
            Subsystem = $_.Subsystem
            Message = $_.Message
        }
    })
}

$forgeSummary |
    ConvertTo-Json -Depth 10 |
    Set-Content -LiteralPath (Join-Path $paths.ReportsRoot 'ForgeSummary.json') -Encoding UTF8

Write-Log SUCCESS 'Forge Complete'
Write-Log INFO ("Analyzed {0} installed mod(s) in {1:N2} seconds." -f @($mods).Count, $totalSeconds)
Write-Log INFO ("Stages: {0} completed, {1} completed with a non-success outcome." -f $completedStageCount, $nonCompletedStageCount)

if ($profileAnalysisMode -eq 'LibraryOnly') {
    Write-Log RECOVERABLE 'No usable profile was available. Profile-specific analysis was skipped; library analysis completed.'
}

if (($recoverableCount + $warningConditionCount + $fatalConditionCount) -eq 0) {
    Write-Log SUCCESS 'No audit conditions require attention.'
}
else {
    Write-Log WARNING ("Conditions: {0} recoverable, {1} warning, {2} fatal." -f $recoverableCount, $warningConditionCount, $fatalConditionCount)
    foreach ($condition in @($script:AuditConditions | Select-Object -First 5)) {
        Write-Log $condition.Severity ("[{0}] {1}" -f $condition.Subsystem, $condition.Message)
    }
    if (@($script:AuditConditions).Count -gt 5) {
        Write-Log INFO ("{0} additional condition(s) are listed in AuditConditions.json." -f (@($script:AuditConditions).Count - 5))
    }
}

Write-Log INFO 'Detailed results are available in Output\Reports.'
