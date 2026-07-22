$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projection = Get-Content (Join-Path $repoRoot 'src\RimForge.Infrastructure\Services\ForgeGraphProjectionService.cs') -Raw
$sorter = Get-Content (Join-Path $repoRoot 'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs') -Raw
$analysis = Get-Content (Join-Path $repoRoot 'src\RimForge.Analysis\Services\ModAnalysisEngine.cs') -Raw

function Assert-Contains([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -notmatch $Pattern) { throw $Message }
}

Assert-Contains $analysis 'AnalysisIssueCode\.DuplicatePackageId' 'Duplicate package IDs are not represented as analysis findings.'
Assert-Contains $projection 'graph\.Nodes\s*\.GroupBy\(' 'Forge graph fingerprint publication is still duplicate-key unsafe.'
Assert-Contains $projection 'GroupBy\(node => node\.PackageId \?\? node\.Id, StringComparer\.OrdinalIgnoreCase\)' 'Forge graph nodes are not grouped by canonical identity.'
Assert-Contains $projection 'group\.OrderBy\(node => node\.Name, StringComparer\.OrdinalIgnoreCase\)' 'Duplicate representative selection is not deterministic.'
if ($projection -match 'node\.IsActive') { throw 'Forge graph projection references the nonexistent DependencyGraphNode.IsActive member.' }
Assert-Contains $sorter 'ActiveProfileMods\s*\.GroupBy\(item => item\.PackageId, StringComparer\.OrdinalIgnoreCase\)' 'Active load-order auto-sort is still duplicate-key unsafe.'
Assert-Contains $sorter 'ProposedOrder\.OrderedPackageIds[\s\S]*?GroupBy\(pair => pair\.packageId, StringComparer\.OrdinalIgnoreCase\)' 'Proposed-order rank publication is still duplicate-key unsafe.'

Write-Host 'PASS: Genuine duplicate package IDs remain findings and no longer abort Forge publication.' -ForegroundColor Green
