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
    "Logging.psm1",
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

# -------------------------------------------------
# Runtime folders and logging
# -------------------------------------------------

$logFolder = Join-Path $ScriptRoot $config.LogFolder
$outputFolder = Join-Path $ScriptRoot $config.OutputFolder
$profilesFolder = Join-Path $ScriptRoot "Profiles"
$optimizedFolder = Join-Path $outputFolder "OptimizedProfiles"
$profileReportsFolder = Join-Path $outputFolder "ProfileReports"
$cacheFolder = Join-Path $ScriptRoot $config.CacheFolder

Initialize-Logger -LogDirectory $logFolder

# Ensure the persistent Profiles folder exists. Profile source files are
# never deleted because this folder is outside Output.
if (-not (Test-Path -LiteralPath $profilesFolder -PathType Container)) {
    New-Item `
        -ItemType Directory `
        -Path $profilesFolder `
        -Force |
        Out-Null
}

# Start every audit with a clean Output folder. Delete only the contents,
# never the Output folder itself.
if (-not (Test-Path -LiteralPath $outputFolder -PathType Container)) {
    New-Item `
        -ItemType Directory `
        -Path $outputFolder `
        -Force |
        Out-Null
}
else {
    $oldOutputItems = @(
        Get-ChildItem `
            -LiteralPath $outputFolder `
            -Force `
            -ErrorAction Stop
    )

    if (@($oldOutputItems).Count -gt 0) {
        Write-Log INFO (
            "Clearing {0} existing item(s) from Output." -f
            @($oldOutputItems).Count
        )

        foreach ($item in $oldOutputItems) {
            Remove-Item `
                -LiteralPath $item.FullName `
                -Recurse `
                -Force `
                -ErrorAction Stop
        }
    }
}

# Recreate generated-output subfolders after cleanup. Cache is persistent.
foreach ($folder in @(
    $optimizedFolder,
    $profileReportsFolder,
    $cacheFolder
)) {
    New-Item `
        -ItemType Directory `
        -Path $folder `
        -Force |
        Out-Null
}

$compatibilityRulesPath = Join-Path `
    $ScriptRoot `
    "CompatibilityRules.json"

$compatibilityRules = Import-CompatibilityRules `
    -Path $compatibilityRulesPath

Write-Log INFO (
    "Loaded {0} custom compatibility rule(s)." -f
    @($compatibilityRules.Rules).Count
)

Write-Log SUCCESS "=============================================="
Write-Log SUCCESS " RimForge v$($config.Version)"
Write-Log INFO " Forging a stable, optimized mod ecosystem."
Write-Log SUCCESS "=============================================="
Write-Log INFO "Configuration loaded."
Write-Log INFO "Modules loaded."

# -------------------------------------------------
# Installed mod library
# -------------------------------------------------

Start-RimForgeTimingStage -Session $timingSession -Name 'Discovery'
$mods = Find-RimWorldMods -RootFolders @($config.RootFolders)
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

$evidenceRulesPath = Join-Path `
    $ScriptRoot `
    "Database\EvidenceRules.json"

$evidenceRules = Import-EvidenceRules `
    -Path $evidenceRulesPath

Write-Log INFO "Scanning mod XML, paths, and assembly metadata for objective evidence."

$evidenceCacheFolder = Join-Path $cacheFolder "Evidence"
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
$evidenceScan = Invoke-ModEvidenceScan `
    -Mods @($mods) `
    -Rules $evidenceRules `
    -OutputFolder $outputFolder `
    -ProgressId 20 `
    -CacheFolder $evidenceCacheFolder `
    -UseCache $useEvidenceCache `
    -TrustedUnchangedPackageIds @($trustedEvidencePackageIds) `
    -TargetVersion $targetVersion
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

$profiles = Find-ModsConfigProfiles `
    -ProfilesFolder $profilesFolder

if (@($profiles).Count -lt 1) {
    throw (
        "No valid ModsConfig profiles were found in '{0}'." -f
        $profilesFolder
    )
}

Write-Log SUCCESS (
    "Discovered {0} valid profile(s)." -f
    @($profiles).Count
)

$loadOrderRulesPath = Join-Path `
    $ScriptRoot `
    "Database\LoadOrderRules.json"

$loadOrderKnowledge = Import-LoadOrderKnowledgeRules `
    -Path $loadOrderRulesPath `
    -TargetVersion $targetVersion

Write-Log INFO (
    "Loaded {0} curated load-order rule(s)." -f
    @($loadOrderKnowledge.Rules).Count
)

$taxonomyPath = Join-Path `
    $ScriptRoot `
    "Database\ModTaxonomy.json"

$taxonomyRulesPath = Join-Path `
    $ScriptRoot `
    "Database\TaxonomyRules.json"

$familyRulesPath = Join-Path `
    $ScriptRoot `
    "Database\FamilyRules.json"

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
    throw "Mod taxonomy database validation failed."
}

$blueprintPath = Join-Path `
    $ScriptRoot `
    "Database\LoadOrderBlueprint.json"

$loadOrderBlueprint = Import-LoadOrderBlueprint `
    -Path $blueprintPath

$blueprintOverridesPath = Join-Path `
    $ScriptRoot `
    "Database\BlueprintOverrides.json"

$blueprintOverrides = Import-BlueprintOverrides `
    -Path $blueprintOverridesPath

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

$versionStatus = Test-ModVersionStatus `
    -Mods @($mods) `
    -TargetVersion $targetVersion `
    -CacheFolder $cacheFolder `
    -CacheHours 24 `
    -ExternalTimeoutSeconds $externalTimeoutSeconds

if ($versionStatus.ExternalChecksSkipped) {
    Write-Log WARNING (
        "One or more external database checks timed out or were unavailable. " +
        "Cached data was used when available; remaining online checks were skipped."
    )
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

$profileVersionSummaries = @(
    foreach ($result in @($profileResults)) {
        $result.VersionStatus
    }
)

Write-VersionStatusReports `
    -VersionStatus $versionStatus `
    -ProfileSummaries @($profileVersionSummaries) `
    -OutputFolder $outputFolder |
    Out-Null

$profileTaxonomySummaries = @(
    foreach ($result in @($profileResults)) {
        $result.Taxonomy
    }
)

Write-TaxonomyReports `
    -Validation $taxonomyValidation `
    -ProfileSummaries @($profileTaxonomySummaries) `
    -OutputFolder $outputFolder |
    Out-Null

foreach ($summary in @($profileTaxonomySummaries)) {
    Write-Log INFO (
        "{0}: taxonomy coverage {1}% ({2}/{3})" -f
        $summary.ProfileName,
        $summary.CoveragePercent,
        $summary.ClassifiedCount,
        $summary.ActiveModCount
    )
}

$profileBlueprintResults = @(
    foreach ($result in @($profileResults)) {
        $result.Blueprint
    }
)

Write-BlueprintReports `
    -Blueprint $loadOrderBlueprint `
    -ProfileBlueprintResults @($profileBlueprintResults) `
    -OutputFolder $outputFolder |
    Out-Null

foreach ($result in @($profileBlueprintResults)) {
    Write-Log INFO (
        "{0}: blueprint score {1}% with {2} minimal section move(s)." -f
        $result.ProfileName,
        $result.BlueprintScore,
        $result.MinimalMoveCount
    )
}

# -------------------------------------------------
# Build persistent GitHub-ready generated database
# -------------------------------------------------

$databaseBuilderEnabled = $true
$generatedDatabaseFolder = Join-Path `
    $ScriptRoot `
    "Database.Generated"

if ($config.PSObject.Properties.Name -contains "DatabaseBuilder") {
    if (
        $config.DatabaseBuilder.PSObject.Properties.Name -contains
        "Enabled"
    ) {
        $databaseBuilderEnabled = [bool]$config.DatabaseBuilder.Enabled
    }

    if (
        $config.DatabaseBuilder.PSObject.Properties.Name -contains
        "OutputFolder" -and
        -not [string]::IsNullOrWhiteSpace(
            [string]$config.DatabaseBuilder.OutputFolder
        )
    ) {
        $configuredDatabaseFolder = [string]$config.DatabaseBuilder.OutputFolder

        if ([System.IO.Path]::IsPathRooted($configuredDatabaseFolder)) {
            $generatedDatabaseFolder = $configuredDatabaseFolder
        }
        else {
            $generatedDatabaseFolder = Join-Path `
                $ScriptRoot `
                $configuredDatabaseFolder
        }
    }
}

$databaseBuild = $null

if ($databaseBuilderEnabled) {
    Write-Log INFO (
        "Building generated mod database at {0}" -f
        $generatedDatabaseFolder
    )

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
        foreach ($errorMessage in @($databaseValidation.Errors)) {
            Write-Log ERROR $errorMessage
        }

        throw "Generated database validation failed."
    }

    Write-Log SUCCESS (
        "Generated database: records={0}, written={1}, unchanged={2}, review={3}, quarantine={4}" -f
        $databaseBuild.RecordCount,
        $databaseBuild.WrittenCount,
        $databaseBuild.UnchangedCount,
        $databaseBuild.ReviewCount,
        $databaseBuild.QuarantineCount
    )
}
else {
    Write-Log INFO "Generated database builder is disabled."
}

# -------------------------------------------------
# Compare the full profile set when more than one exists
# -------------------------------------------------

$profileSetComparison = $null

if (@($profiles).Count -gt 1) {
    $profileSetComparison = Compare-ProfileSet `
        -Profiles @($profiles)

    Write-ProfileSetReports `
        -Comparison $profileSetComparison `
        -ProfileResults @($profileResults) `
        -OutputFolder $outputFolder |
        Out-Null

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
else {
    Write-Log INFO "Only one profile was found; cross-profile comparison skipped."
}

# -------------------------------------------------
# Global audit report
# -------------------------------------------------

Write-AuditReport `
    -Mods @($mods) `
    -Validation $validation `
    -DependencyGraph $dependencyGraph `
    -OutputFolder $outputFolder `
    -Version $config.Version |
    Out-Null

# Optional combined summary for the refactored pipeline.
$pipelineSummary = [PSCustomObject]@{
    Generated            = (Get-Date).ToString("o")
    ProfileCount         = @($profiles).Count
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
        -LiteralPath (Join-Path $outputFolder "ProfilePipeline.json") `
        -Encoding UTF8

$incrementalRunSummary |
    ConvertTo-Json -Depth 20 |
    Set-Content -LiteralPath (Join-Path $outputFolder 'IncrementalAudit.json') -Encoding UTF8

Write-Log INFO ("Incremental savings: skipped About.xml={0}, skipped evidence scans={1}." -f $aboutImport.CacheHitCount,$evidenceScan.CacheHitCount)
Write-Log INFO ("Audit timing: total={0:N2}s, discovery={1:N2}s, fingerprint={2:N2}s, about={3:N2}s, evidence={4:N2}s." -f ($timingSummary.TotalMilliseconds/1000),($timingSummary.Stages.Discovery/1000),($timingSummary.Stages.Fingerprinting/1000),($timingSummary.Stages.AboutMetadata/1000),($timingSummary.Stages.EvidenceScan/1000))
Write-Log SUCCESS "Startup complete."
