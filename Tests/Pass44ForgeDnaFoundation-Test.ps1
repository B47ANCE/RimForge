$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$models = Get-Content (Join-Path $root 'src/RimForge.Analysis/Models/ForgeDnaModels.cs') -Raw
$service = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/ForgeDnaService.cs') -Raw
$contract = Get-Content (Join-Path $root 'src/RimForge.Analysis/Services/IForgeDnaService.cs') -Raw
$composition = Get-Content (Join-Path $root 'src/RimForge.App/Composition/RimForgeApplicationServices.cs') -Raw
$runner = Get-Content (Join-Path $root 'src/RimForge.App/Forge/NativeForgeRunner.cs') -Raw
$main = Get-Content (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs') -Raw

$checks = @(
    @{ Name = 'Canonical ForgeDnaRecord exists'; Pass = $models -match 'sealed record ForgeDnaRecord' },
    @{ Name = 'Snapshot carries shared analysis'; Pass = $models -match 'ModAnalysisSnapshot Analysis' },
    @{ Name = 'Fingerprint supports incremental reuse'; Pass = $models -match 'ForgeDnaFingerprint' -and $service -match '_cache' },
    @{ Name = 'Service supports cancellation'; Pass = $contract -match 'CancellationToken' -and $service -match 'ThrowIfCancellationRequested' },
    @{ Name = 'Service supports invalidation'; Pass = $contract -match 'void Invalidate' -and $service -match 'public void Invalidate' },
    @{ Name = 'Composition owns one Forge DNA service'; Pass = $composition -match 'var forgeDnaService = new ForgeDnaService\(analysisEngine\)' },
    @{ Name = 'Startup projection consumes Forge DNA'; Pass = $main -match '_forgeDnaService\.AnalyzeAsync' },
    @{ Name = 'Native Forge emits Forge DNA report'; Pass = $runner -match 'ForgeDnaReport\.json' }
)

$failed = $checks | Where-Object { -not $_.Pass }
$checks | ForEach-Object { Write-Host ("[{0}] {1}" -f ($(if ($_.Pass) { 'PASS' } else { 'FAIL' })), $_.Name) }
if ($failed.Count -gt 0) { throw "Pass 44 Forge DNA foundation failed $($failed.Count) acceptance check(s)." }
Write-Host 'Pass 44 Forge DNA foundation acceptance checks passed.'
