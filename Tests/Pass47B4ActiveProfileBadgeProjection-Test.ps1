$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$vm = Get-Content (Join-Path $repo 'src\RimForge.UI\ViewModels\ProfileLoadOrderItemViewModel.cs') -Raw
$xaml = Get-Content (Join-Path $repo 'src\RimForge.App\Features\ModSorter\ModSorterView.xaml') -Raw
$checks = @(
  @{ Name='row-owned visible badge projection'; Ok=$vm.Contains('VisibleEvidenceBadges =>') },
  @{ Name='row-owned hidden badge projection'; Ok=$vm.Contains('HasHiddenEvidenceBadges =>') },
  @{ Name='explicit visible badge notification'; Ok=$vm.Contains('Notify(nameof(VisibleEvidenceBadges))') },
  @{ Name='active/inactive templates bind row projection'; Ok=([regex]::Matches($xaml, 'ItemsSource="\{Binding VisibleEvidenceBadges\}"').Count -ge 2) },
  @{ Name='templates no longer bind nested visible badges'; Ok=(-not $xaml.Contains('ItemsSource="{Binding Mod.Evidence.VisibleBadges}"')) }
)
$failed = $checks | Where-Object { -not $_.Ok }
$checks | ForEach-Object { Write-Host (('{0}: {1}' -f $_.Name, $(if ($_.Ok) {'PASS'} else {'FAIL'}))) }
if ($failed) { throw "Pass47B4 active profile badge projection gate failed." }
Write-Host 'Pass47B4 active profile badge projection gate passed.' -ForegroundColor Green
