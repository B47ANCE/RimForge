$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rules = Get-Content (Join-Path $root 'src/RimForge.Core/Models/LoadOrderRules.cs') -Raw
$item = Get-Content (Join-Path $root 'src/RimForge.UI/ViewModels/ProfileLoadOrderItemViewModel.cs') -Raw
$sorter = Get-Content (Join-Path $root 'src/RimForge.App/Features/ModSorter/MainWindow.ModSorter.cs') -Raw

$checks = @(
    @{ Text = $rules; Token = 'public static bool IsPositionAnchor' },
    @{ Text = $rules; Token = 'public static bool IsMandatory(string? packageId) => IsCore(packageId);' },
    @{ Text = $rules; Token = 'public static bool CanDeactivate(string? packageId) => !IsMandatory(packageId);' },
    @{ Text = $item; Token = 'public bool IsMandatory => LoadOrderRules.IsMandatory(PackageId);' },
    @{ Text = $item; Token = 'public bool CanDeactivate => LoadOrderRules.CanDeactivate(PackageId);' },
    @{ Text = $sorter; Token = 'items.Any(item => item.IsMandatory)' },
    @{ Text = $sorter; Token = 'payload.FromActive && items.Any(item => item.IsLoadOrderAnchor)' },
    @{ Text = $sorter; Token = 'LoadOrderRules.CanDeactivate(reason.PackageId)' }
)

foreach ($check in $checks) {
    if (-not $check.Text.Contains($check.Token)) {
        throw "Missing Pass 39 anchor semantics behavior: $($check.Token)"
    }
}

if ($sorter.Contains('Canonical load-order anchors cannot be disabled.')) {
    throw 'Legacy anchor-wide deactivation block is still present.'
}

Write-Host 'Pass 39 anchor semantics validation passed.' -ForegroundColor Green
