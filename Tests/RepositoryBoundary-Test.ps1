$ErrorActionPreference = 'Stop'
$client = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent $client
$companion = Join-Path $workspace 'RimForge.Companion'
$suite = Join-Path $workspace 'RimForge.Companion.TestSuite'

if (-not (Test-Path -LiteralPath $client -PathType Container)) {
    throw "Canonical Client repository root is missing: $client"
}
$companionExists = Test-Path -LiteralPath $companion -PathType Container
$suiteExists = Test-Path -LiteralPath $suite -PathType Container
if ($companionExists -ne $suiteExists) {
    throw 'Canonical sibling checkout is incomplete; Companion and Test Suite must either both be present or both be absent.'
}

foreach ($relative in @('src/RimForge.Agent','mods/RimForge.Runtime','test-assets','tests/RimForge.Runtime.TestHarness','tests/RimForge.Protocol.Tests','build/companion')) {
    if (Test-Path -LiteralPath (Join-Path $client $relative)) {
        throw "Companion or runtime-test content leaked into RimForge.Client: $relative"
    }
}
if ($companionExists) {
    foreach ($relative in @('src/RimForge.Agent/RimForge.Agent.csproj','mods/RimForge.Runtime/About/About.xml','build/companion/Package-Mod.ps1')) {
        if (-not (Test-Path -LiteralPath (Join-Path $companion $relative) -PathType Leaf)) {
            throw "RimForge.Companion ownership is incomplete: $relative"
        }
    }
    foreach ($relative in @('tests/RimForge.Runtime.TestHarness/RimForge.Runtime.TestHarness.csproj','tests/RimForge.Protocol.Tests/RimForge.Protocol.Tests.csproj','test-assets/expected-runtime-evidence.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $suite $relative) -PathType Leaf)) {
            throw "RimForge.Companion.TestSuite ownership is incomplete: $relative"
        }
    }
    if (Test-Path -LiteralPath (Join-Path $companion 'src/RimForge.Companion.Host')) {
        throw 'The desktop Companion Host must remain owned by RimForge.Client.'
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $client 'src/RimForge.Companion.Host/RimForge.Companion.Host.csproj'))) {
    throw 'RimForge.Client is missing its Companion Host/bootstrapper.'
}

Write-Output 'Three-repository ownership boundary verified.'
