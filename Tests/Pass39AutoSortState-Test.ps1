$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$main = Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw
$sorter = Get-Content (Join-Path $root 'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs') -Raw
$bar = Get-Content (Join-Path $root 'src\RimForge.UI\Controls\ForgeLaunchBar.xaml') -Raw

$checks = @(
    @{ Name='Auto-Sort exposes Enabled/Disabled state text'; Value=$main.Contains('public string AutoSortStateText => IsInstantAutoSortEnabled ? "Enabled" : "Disabled";') },
    @{ Name='Auto-Sort notifies its subtitle'; Value=$main.Contains('Notify(nameof(AutoSortStateText));') },
    @{ Name='Toggle binding is explicitly two-way'; Value=$bar.Contains('IsChecked="{Binding IsInstantAutoSortEnabled, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"') },
    @{ Name='Sort can run without an analysis snapshot'; Value=(-not $sorter.Contains('if (SelectedProfile is null || _analysisSnapshot is null) return false;')) },
    @{ Name='Canonical state uses the same order calculation as Sort'; Value=$sorter.Contains('var canonicalOrder = CalculateCanonicalLoadOrder();') }
)

$failed = $checks | Where-Object { -not $_.Value }
if ($failed) {
    $failed | ForEach-Object { Write-Error "FAILED: $($_.Name)" }
    exit 1
}
Write-Host 'Pass 39 Auto-Sort state validation passed.' -ForegroundColor Green
