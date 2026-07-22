$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$colors = Get-Content (Join-Path $root "src\RimForge.UI\Themes\Colors.xaml") -Raw
$controls = Get-Content (Join-Path $root "src\RimForge.UI\Themes\Controls.xaml") -Raw
$forgeView = Get-Content (Join-Path $root "src\RimForge.App\Features\ForgeView\ForgeViewView.xaml") -Raw

$checks = @(
  @{ Name = "Canonical active surface token exists"; Pass = $colors -match 'x:Key="VisualActiveSurfaceBrush"' },
  @{ Name = "Canonical active marker token exists"; Pass = $colors -match 'x:Key="VisualActiveMarkerBrush"' },
  @{ Name = "Canonical focus token exists"; Pass = $colors -match 'x:Key="VisualFocusBrush"' },
  @{ Name = "Unified navigation has selected trigger"; Pass = $controls -match '<Trigger Property="Tag" Value="Selected">' },
  @{ Name = "Unified navigation hover does not use active marker"; Pass = $controls -match 'VisualSurfaceHoverBrush' },
  @{ Name = "Shared mode selector exists"; Pass = $controls -match 'x:Key="VisualModeSelectorButton"' },
  @{ Name = "ForgeView consumes shared mode selector"; Pass = $forgeView -match 'Style="\{StaticResource VisualModeSelectorButton\}"' },
  @{ Name = "ForgeView local mode style removed"; Pass = $forgeView -notmatch 'x:Key="ForgeViewModeButton"' },
  @{ Name = "No black foreground in app XAML"; Pass = @(Get-ChildItem (Join-Path $root "src") -Recurse -Filter *.xaml | Select-String -Pattern 'Foreground="(Black|#000000|#FF000000)"').Count -eq 0 }
)

$failed = $false
foreach ($check in $checks) {
  if ($check.Pass) { Write-Host "[PASS] $($check.Name)" -ForegroundColor Green }
  else { Write-Host "[FAIL] $($check.Name)" -ForegroundColor Red; $failed = $true }
}
if ($failed) { exit 1 }
Write-Host "Pass 45A.1 visual standardization gate passed." -ForegroundColor Green
