$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$text=(Get-Content (Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs') -Raw)+"`n"+(Get-Content (Join-Path $root 'src\RimForge.App\Features\SharedEvidence\MainWindow.SharedEvidence.cs') -Raw)
if(-not $text.Contains('ApplyBackgroundIntelligenceUpdate(mod)')){throw 'Incremental intelligence updates are not applied.'}
$service=Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ModLibraryService.cs') -Raw
if(-not $service.Contains('enrichedModProgress?.Report(mod)')){throw 'Mod library does not publish enriched mods.'}
Write-Host 'Progressive intelligence test passed.'
