$root = Split-Path -Parent $PSScriptRoot
$policy = Get-Content (Join-Path $root 'src\RimForge.Core\Models\LoadOrderPolicy.cs') -Raw
$pack = Get-Content (Join-Path $root 'src\RimForge.Core\Models\LoadOrderPolicyPack.cs') -Raw
$category = Get-Content (Join-Path $root 'src\RimForge.Core\Models\LoadOrderCategory.cs') -Raw
$analysis = Get-Content (Join-Path $root 'src\RimForge.Analysis\Services\ModAnalysisEngine.cs') -Raw
$json = Get-Content (Join-Path $root 'Database.Curated\LoadOrderRules.json') -Raw
$app = Get-Content (Join-Path $root 'src\RimForge.App\RimForge.App.csproj') -Raw

foreach ($token in @(
    'Later bands override earlier bands',
    'LoadOrderRuleConfidence',
    'CandidateCategories',
    'ClassificationEvidence',
    'LoadOrderPolicyPack.LoadDefault',
    'GetApplicableRelativeRules',
    'Explain(ModRecord mod)')) {
    if (-not (($policy + $pack + $category).Contains($token))) { throw "Missing load-order knowledge-engine token: $token" }
}

foreach ($token in @('"PackageRules"', '"RelativeRules"', '"Confidence": "Hard"', '"Confidence": "Recommended"', '"Confidence": "Experimental"')) {
    if (-not $json.Contains($token)) { throw "Missing rule-pack token: $token" }
}

if (-not $analysis.Contains('LoadOrderPolicy.GetApplicableRelativeRules')) { throw 'Analysis engine does not apply curated relative rules.' }
if (-not $app.Contains('Database.Curated\LoadOrderRules.json')) { throw 'Rule pack is not copied to application output.' }

Write-Host 'Pass45A13LoadOrderKnowledgeEngine-Test: PASSED'
