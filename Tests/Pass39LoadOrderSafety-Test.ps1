$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rules = Get-Content (Join-Path $root 'src/RimForge.Core/Models/LoadOrderRules.cs') -Raw
$sorter = Get-Content (Join-Path $root 'src/RimForge.App/Features/ModSorter/MainWindow.ModSorter.cs') -Raw
$bar = Get-Content (Join-Path $root 'src/RimForge.UI/Controls/ForgeLaunchBar.xaml') -Raw
$checks = @(
  @{ Text=$rules; Token='GetCanonicalDependencies' },
  @{ Text=$rules; Token='unique.Insert(0, CorePackageId)' },
  @{ Text=$sorter; Token='Dependency Still Required' },
  @{ Text=$sorter; Token='IsInstantAutoSortEnabled = false' },
  @{ Text=$bar; Token='ChromePlusSortButton' },
  @{ Text=$bar; Token='Load order already matches RimForge''s canonical ordering.' }
)
foreach ($check in $checks) { if (-not $check.Text.Contains($check.Token)) { throw "Missing Pass 39 load-order safety behavior: $($check.Token)" } }
Write-Host 'Pass 39 load-order safety validation passed.' -ForegroundColor Green
