$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root 'src\RimForge.UI\Assets\Branding\AssetManifest.json'
if (-not (Test-Path $manifestPath)) { throw "Missing brand asset manifest: $manifestPath" }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -lt 1) { throw 'Invalid schemaVersion.' }
if ($manifest.brandInvariant -ne 'CompactBadge') { throw 'Brand invariant must be CompactBadge.' }
if ($manifest.compactIdentity.status -ne 'ApprovedCanonical') { throw 'Compact badge must be canonical.' }
if ($manifest.compactIdentity.sourceAsset -ne 'Branding/Badge/RimForge.Badge.png') { throw 'Unexpected compact badge source.' }
if ($manifest.compactIdentity.windowsIcon -ne 'Branding/Badge/RimForge.Badge.ico') { throw 'Unexpected Windows icon derivative.' }
foreach ($asset in @($manifest.compactIdentity.sourceAsset, $manifest.compactIdentity.windowsIcon)) {
    $path = Join-Path (Join-Path $root 'src\RimForge.UI\Assets') $asset
    if (-not (Test-Path $path)) { throw "Missing compact identity asset: $asset" }
}
$ids = @($manifest.workflows | ForEach-Object id)
if ($ids.Count -ne (@($ids | Sort-Object -Unique)).Count) { throw 'Workflow IDs must be unique.' }
$accessories = @($manifest.workflows | Where-Object id -ne 'LegacyAnvil' | ForEach-Object accessory)
if ($accessories.Count -ne (@($accessories | Sort-Object -Unique)).Count) { throw 'Workflow accessories must be unique.' }
foreach ($workflow in $manifest.workflows) {
    if ([string]::IsNullOrWhiteSpace($workflow.id)) { throw 'Workflow id is required.' }
    if ([string]::IsNullOrWhiteSpace($workflow.asset)) { throw "Asset path is required for $($workflow.id)." }
    if (-not $workflow.asset.EndsWith('.svg')) { throw "Authoritative asset must be SVG for $($workflow.id)." }
}
Write-Host 'Brand asset manifest test passed.'
