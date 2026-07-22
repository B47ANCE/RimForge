$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$analysisModels = Get-Content (Join-Path $root 'src/RimForge.Analysis/Models/AnalysisModels.cs') -Raw
$analysisEngine = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/ModAnalysisEngine.cs') -Raw
$issueEngine = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/IssueEngine.cs') -Raw
$repairPlanner = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/RepairPlanner.cs') -Raw
$viewModel = Get-Content (Join-Path $root 'src/RimForge.UI/ViewModels/ModSorterItemViewModel.cs') -Raw
$inspector = Get-Content (Join-Path $root 'src/RimForge.App/Features/ModInspector/ModInspectorView.xaml') -Raw

$requiredContracts = @(
    @{ Text = $analysisModels; Pattern = 'public sealed record LoadOrderDecision'; Message = 'Load-order decision provenance model is missing.' },
    @{ Text = $analysisModels; Pattern = 'InactiveRequiredDependency'; Message = 'Inactive required dependency finding is missing.' },
    @{ Text = $analysisEngine; Pattern = 'var scopedHardOrdering = FilterAdjacency'; Message = 'Sort graph is not isolated to the active profile.' },
    @{ Text = $analysisEngine; Pattern = 'Where\(item => !item\.IsMandatory\)'; Message = 'Recommended rules are not separated from mandatory graph edges.' },
    @{ Text = $analysisEngine; Pattern = 'rule\.Confidence == LoadOrderRuleConfidence\.Hard'; Message = 'Curated rule confidence is not used to gate hard edges.' },
    @{ Text = $analysisEngine; Pattern = 'Decisions = decisions'; Message = 'Topological plan does not publish decision provenance.' },
    @{ Text = $analysisEngine; Pattern = 'AddInactiveDependencyIssues'; Message = 'Active-profile dependency closure is not validated.' },
    @{ Text = $issueEngine; Pattern = 'RepairActionKind\.ActivateDependency'; Message = 'Inactive dependency issue is not projected into Issue Viewer.' },
    @{ Text = $repairPlanner; Pattern = 'BuildActivationPlan'; Message = 'Inactive dependency repair planning is missing.' },
    @{ Text = $viewModel; Pattern = 'SortMovementText'; Message = 'Mod Sorter projection lacks movement explanation.' },
    @{ Text = $inspector; Pattern = 'LOAD-ORDER DECISION'; Message = 'Mod Inspector does not surface sort provenance.' }
)

foreach ($contract in $requiredContracts) {
    if ($contract.Text -notmatch $contract.Pattern) {
        throw $contract.Message
    }
}

if ($analysisEngine -match 'if \(mandatory\) AddUnique\(hardOrdering\[before\], after\);[\s\S]{0,500}FindCycles\(orderingPreferences\)') {
    throw 'Preference-only rules appear to participate in hard-cycle detection.'
}

Write-Host 'Tri-hybrid sorting pipeline contracts passed.' -ForegroundColor Green
