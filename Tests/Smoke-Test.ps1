Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$moduleFolder = Join-Path $root 'Modules'

$moduleOrder = @(
    'ProjectValidation.psm1','Logging.psm1','CacheService.psm1','FingerprintService.psm1','IncrementalAudit.psm1','ModRecord.psm1','Discovery.psm1',
    'AboutParser.psm1','IndexBuilder.psm1','Validator.psm1','DependencyGraph.psm1',
    'EvidenceScanner.psm1','TextureOptimizer.psm1','DependencyManager.psm1','ProfileDiscovery.psm1','ProfileValidation.psm1',
    'ProfileComparison.psm1','CompatibilityScanner.psm1','KnowledgeRules.psm1',
    'TaxonomyManager.psm1','BlueprintScoring.psm1','BlueprintAnalyzer.psm1',
    'LoadOrderOptimizer.psm1','VersionChecker.psm1','DatabaseBuilder.psm1',
    'ReportWriter.psm1'
)

foreach ($name in $moduleOrder) {
    Import-Module (Join-Path $moduleFolder $name) -Force -DisableNameChecking -ErrorAction Stop
}

$contracts = Test-RimForgeModuleContracts
if (-not $contracts.IsValid) {
    throw "Missing commands: $(@($contracts.MissingCommands) -join ', ')"
}

$json = Test-RimForgeJsonFiles -Root $root
if (-not $json.IsValid) {
    throw "Invalid JSON files: $(@($json.Errors.Path) -join ', ')"
}

$config = Get-Content (Join-Path $root 'Config.json') -Raw | ConvertFrom-Json
$configResult = Test-RimForgeConfiguration -Config $config -ScriptRoot $root
if (-not $configResult.IsValid) {
    throw ($configResult.Errors -join [Environment]::NewLine)
}

Write-Host 'RimForge smoke test passed.' -ForegroundColor Green
