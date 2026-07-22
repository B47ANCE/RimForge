$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$busPath = Join-Path $root 'src\RimForge.Core\Services\ApplicationEventBus.cs'
$bus = Get-Content $busPath -Raw
foreach ($token in @('lock (_gate)', 'registered.Cast<Action<TEvent>>().ToArray()', 'Interlocked.Exchange(ref _unsubscribe, null)', 'public void Dispose()', '_subscriptions.Clear()')) {
    if (-not $bus.Contains($token)) { throw "Event bus safety contract missing: $token" }
}
if ($bus.Contains('async void')) { throw 'Event bus contains async void behavior.' }
Write-Host 'Event bus architecture validation passed.'
