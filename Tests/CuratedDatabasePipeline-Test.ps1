$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$policy = Get-Content (Join-Path $root 'Database.Curated/LoadOrderRules.json') -Raw | ConvertFrom-Json
if ($policy.SchemaVersion -lt 4) { throw 'LoadOrderRules schema must be version 4 or newer.' }
if ([string]::IsNullOrWhiteSpace($policy.ContentVersion)) { throw 'LoadOrderRules is missing ContentVersion.' }

$ids = @()
foreach ($rule in @($policy.PackageRules) + @($policy.RelativeRules)) {
    if ([string]::IsNullOrWhiteSpace($rule.RuleId)) { throw 'Every curated load-order rule must have RuleId.' }
    if ([string]::IsNullOrWhiteSpace($rule.Source)) { throw "Rule $($rule.RuleId) is missing Source." }
    if ($null -eq $rule.Scope) { throw "Rule $($rule.RuleId) is missing version Scope." }
    $ids += $rule.RuleId
}
if (($ids | Select-Object -Unique).Count -ne $ids.Count) { throw 'Curated load-order RuleId values must be unique.' }

$replacement = Get-Content (Join-Path $root 'Database.Curated/UseThisInstead.json') -Raw | ConvertFrom-Json
if ($replacement.SchemaVersion -ne 1) { throw 'UseThisInstead schema version must be 1.' }
if ($null -eq $replacement.Rules) { throw 'UseThisInstead must contain a Rules array.' }

$engine = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/ModAnalysisEngine.cs') -Raw
@('targetRimWorldVersion', 'FindContradictoryCuratedRules', 'AddUseThisInsteadIssues', 'CuratedRuleConflict', 'ReplacementRecommended') |
    ForEach-Object { if (-not $engine.Contains($_)) { throw "Analysis engine is missing curated database contract: $_" } }

$appProject = Get-Content (Join-Path $root 'src/RimForge.App/RimForge.App.csproj') -Raw
if (-not $appProject.Contains('Database.Curated\UseThisInstead.json')) { throw 'UseThisInstead database is not copied to application output.' }

Write-Host 'Curated database pipeline contract passed.' -ForegroundColor Green
