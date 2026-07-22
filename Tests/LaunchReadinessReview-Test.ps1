$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$text=(Get-Content (Join-Path $root 'src\RimForge.App\Features\LaunchBar\MainWindow.LaunchBar.cs') -Raw)+"`n"+(Get-Content (Join-Path $root 'src\RimForge.UI\Dialogs\ForgeDialogService.cs') -Raw)
foreach($token in @('ShowLaunchReadinessReview','LaunchReadiness')){if(-not $text.Contains($token)){throw "Launch readiness review missing: $token"}}
Write-Host 'Launch readiness review validation passed.'
