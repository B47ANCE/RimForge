$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$timelineSource = Join-Path $root 'src\RimForge.App\Startup\StartupTimeline.cs'
$mainWindowSource = Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs'
$appSource = Join-Path $root 'src\RimForge.App\App.xaml.cs'

foreach ($path in @($timelineSource, $mainWindowSource, $appSource)) {
    if (-not (Test-Path $path)) { throw "Missing startup instrumentation source: $path" }
}

$timeline = Get-Content $timelineSource -Raw
$window = Get-Content $mainWindowSource -Raw
$app = Get-Content $appSource -Raw

$requiredTimelineTokens = @(
    '[ModuleInitializer]',
    'Process.GetCurrentProcess().StartTime',
    'StartupTimelineReport',
    'StartupTimelineEvent',
    'File.WriteAllTextAsync'
)
foreach ($token in $requiredTimelineTokens) {
    if ($timeline -notmatch [regex]::Escape($token)) { throw "Startup timeline is missing: $token" }
}

$requiredMarkers = @(
    'App constructor entered',
    'MainWindow constructor entered',
    'MainWindow InitializeComponent started',
    'Application service composition started',
    'First content render',
    'Startup coordinator entered',
    'Usable UI ready',
    'StartupTimeline.json'
)
$combined = $app + "`n" + $window
foreach ($marker in $requiredMarkers) {
    if ($combined -notmatch [regex]::Escape($marker)) { throw "Startup marker is missing: $marker" }
}

Write-Host 'Startup timeline instrumentation test passed.' -ForegroundColor Green
