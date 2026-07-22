$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$servicePath = Join-Path $root 'src\RimForge.Core\Services\NotificationService.cs'
$compositionPath = Join-Path $root 'src\RimForge.App\Composition\RimForgeApplicationServices.cs'
$mainPath = Join-Path $root 'src\RimForge.App\MainWindow.xaml.cs'
$xamlPath = Join-Path $root 'src\RimForge.App\MainWindow.xaml'

foreach ($path in @($servicePath, $compositionPath, $mainPath, $xamlPath)) {
    if (-not (Test-Path $path)) { throw "Missing Control Center notification foundation file: $path" }
}

$service = Get-Content $servicePath -Raw
$composition = Get-Content $compositionPath -Raw
$main = Get-Content $mainPath -Raw
$xaml = Get-Content $xamlPath -Raw

foreach ($token in @(
    'interface INotificationService',
    'NotificationSeverity',
    'NotificationRequest',
    'NotificationSnapshot',
    'NotificationRequestedEvent',
    'NotificationActionInvokedEvent',
    'BackgroundTaskChangedEvent',
    'ForgeSessionChangedEvent',
    'QueuedNotification',
    'GetPriority',
    'DisplayDuration'
)) {
    if (-not $service.Contains($token)) { throw "Notification service is missing: $token" }
}

foreach ($token in @(
    'INotificationService NotificationService',
    'new NotificationService(eventBus)',
    'NotificationService.Dispose()'
)) {
    if (-not $composition.Contains($token)) { throw "Notification service composition is missing: $token" }
}

foreach ($token in @(
    'NotificationChangedEvent',
    'NotificationService_NotificationChanged',
    'ControlCenterNotificationAction_Click',
    'DismissControlCenterNotification_Click',
    'IsControlCenterNotificationVisible'
)) {
    if (-not $main.Contains($token)) { throw "MainWindow notification integration is missing: $token" }
}

foreach ($token in @(
    'x:Name="ForgeNotificationBar"',
    'x:Name="ControlCenter"',
    'CurrentNotification.AvailableActions',
    'ControlCenterNotificationAction_Click',
    'DismissControlCenterNotification_Click'
)) {
    if (-not $xaml.Contains($token)) { throw "Control Center notification surface is missing: $token" }
}

if ($service.Contains('MessageBox.Show')) { throw 'Notification service must remain non-modal.' }
Write-Host 'Control Center notification foundation validation passed.'
