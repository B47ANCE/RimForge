Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$service = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\DependencyGraphService.cs') -Raw
$rules = Get-Content (Join-Path $root 'src\RimForge.Core\Models\LoadOrderRules.cs') -Raw
$policy = Get-Content (Join-Path $root 'src\RimForge.App\Features\ForgeView\ForgeGraphPresentationPolicy.cs') -Raw

if ($service -notmatch 'Concat\(LoadOrderRules\.GetCanonicalDependencies\(mod\.PackageId\)\)') {
    throw 'Dependency graph does not include canonical official-content dependencies.'
}
if ($rules -notmatch 'IsOfficialDlc\(packageId\)[\s\S]*new ModDependency\(CorePackageId') {
    throw 'Official DLC does not declare the canonical Core dependency.'
}
if ($policy -notmatch 'LoadOrderRules\.IsOfficialDlc\(otherPackageId\)') {
    throw 'Forge graph presentation does not retain Core-to-DLC edges.'
}
Write-Host 'Core/DLC dependency graph test passed.'
