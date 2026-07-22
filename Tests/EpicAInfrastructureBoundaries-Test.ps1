$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$runtimeHost = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\RuntimeSensorHost.cs') -Raw
$composition = Get-Content (Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs') -Raw
$textureView = Get-Content (Join-Path $root 'src\RimForge.App\Features\TextureTools\TextureToolsView.xaml.cs') -Raw
$textureEngine = Get-Content (Join-Path $root 'src\RimForge.App\Features\TextureTools\TextureConversionEngine.cs') -Raw
$sessionService = Get-Content (Join-Path $root 'src\RimForge.Infrastructure\Services\ForgeSessionService.cs') -Raw

if ($runtimeHost.Contains('Task.Run')) { throw 'RuntimeSensorHost still owns an isolated Task.Run loop.' }
if ($composition -match '_\s*=\s*runtime(EvidenceStore|SensorHost)\..*Async') { throw 'Composition still fire-and-forgets runtime initialization.' }
if (-not $runtimeHost.Contains('IHostedBackgroundWorkService')) { throw 'RuntimeSensorHost does not use the hosted-work boundary.' }
if ($textureView.Contains('SpecialFolder.MyDocuments')) { throw 'TextureToolsView still discovers its own output root.' }
if ($textureEngine.Contains('Path.GetTempPath')) { throw 'TextureConversionEngine still discovers its own scratch root.' }
if ($sessionService.Contains('Directory.GetCurrentDirectory')) { throw 'ForgeSessionService still invents a workspace from the current directory.' }

$scatteredCurrentDirectory = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter '*.cs' -File |
    Select-String -Pattern 'Environment\.CurrentDirectory|Directory\.GetCurrentDirectory' |
    Select-Object -ExpandProperty Path -Unique |
    Where-Object { $_ -notmatch 'RimForgePaths\.cs$' }
if ($scatteredCurrentDirectory) { throw "Current-directory discovery escaped RimForgePathLayout: $($scatteredCurrentDirectory -join ', ')" }

Write-Output 'Epic A infrastructure boundaries verified.'
