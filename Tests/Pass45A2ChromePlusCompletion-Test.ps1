$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$forgeViewXaml = Get-Content (Join-Path $root "src\RimForge.App\Features\ForgeView\ForgeViewView.xaml") -Raw
$forgeCanvas = Get-Content (Join-Path $root "src\RimForge.App\Features\ForgeView\ForgeGraphCanvas.cs") -Raw
$forgeViewCode = Get-Content (Join-Path $root "src\RimForge.App\Features\ForgeView\ForgeViewView.xaml.cs") -Raw
$launchBar = Get-Content (Join-Path $root "src\RimForge.UI\Controls\ForgeLaunchBar.xaml") -Raw
$settings = Get-Content (Join-Path $root "src\RimForge.App\Features\Settings\SettingsView.xaml") -Raw
$props = Get-Content (Join-Path $root "Directory.Build.props") -Raw
$canonicalVersion = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
$passVersionAdvanced = $props -match ('<Version>' + [regex]::Escape($canonicalVersion) + '</Version>')

$checks = @(
  @{ Name = "ForgeView is a unified card"; Pass = $forgeViewXaml -match '<Border Style="\{StaticResource Card\}" Padding="0"' },
  @{ Name = "ForgeView includes workspace status chrome"; Pass = $forgeViewXaml -match 'Minimap: click or drag to focus' },
  @{ Name = "Minimap click navigation exists"; Pass = $forgeCanvas -match 'TryBeginMinimapNavigation' -and $forgeCanvas -match 'NavigateFromMinimap' },
  @{ Name = "Minimap drag navigation exists"; Pass = $forgeCanvas -match '_isMinimapNavigating' -and $forgeCanvas -match 'MouseButtonState.Pressed' },
  @{ Name = "Selection history controls exist"; Pass = $forgeViewXaml -match 'PreviousSelectionButton' -and $forgeViewXaml -match 'NextSelectionButton' },
  @{ Name = "Selection history behavior exists"; Pass = $forgeViewCode -match 'NavigateSelectionHistory' -and $forgeViewCode -match 'RecordSelection' },
  @{ Name = "Launch bar routes profile administration"; Pass = $launchBar -match 'Profile Management' -and $launchBar -notmatch '<local:ForgeProfileToolbar Grid.Column="1"' },
  @{ Name = "Settings owns profile administration"; Pass = $settings -match 'PROFILE MANAGEMENT' -and $settings -match '<forge:ForgeProfileToolbar' },
  @{ Name = "Canonical version is unified"; Pass = $passVersionAdvanced }
)

$failed = $false
foreach ($check in $checks) {
  if ($check.Pass) { Write-Host "[PASS] $($check.Name)" -ForegroundColor Green }
  else { Write-Host "[FAIL] $($check.Name)" -ForegroundColor Red; $failed = $true }
}
if ($failed) { exit 1 }
Write-Host "Pass 45A.2 Chrome+ completion gate passed." -ForegroundColor Green
