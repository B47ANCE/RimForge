$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$launchBarPath = Join-Path $root 'src\RimForge.UI\Controls\ForgeLaunchBar.xaml'
if (-not (Test-Path $launchBarPath)) { throw "Missing Launch Bar XAML: $launchBarPath" }

$launchBarXaml = Get-Content $launchBarPath -Raw
$required = @(
    'Text="PROFILE HEALTH"',
    'Text="LOAD ORDER"',
    'Text="FORGE"',
    'Binding HasActiveProfileIssues',
    'Click="ShowIssues_Click"',
    'Click="FixIssues_Click"',
    'Click="ManualAutoSort_Click"',
    'Click="SaveLoadOrder_Click"',
    'Click="Forge_Click"',
    'Click="Launch_Click"'
)
foreach ($token in $required) {
    if (-not $launchBarXaml.Contains($token)) { throw "Missing Launch Bar polish token: $token" }
}

if ($launchBarXaml -match 'Expanded issue state with direct actions') {
    throw 'Legacy expanded Profile Health card remains in the Launch Bar.'
}
if (($launchBarXaml | Select-String -Pattern 'Text="PROFILE HEALTH"' -AllMatches).Matches.Count -ne 1) {
    throw 'Profile Health must have exactly one presentation surface.'
}
if ($launchBarXaml -notmatch 'DataTrigger Binding="\{Binding HasActiveProfileIssues\}" Value="True"') {
    throw 'Issue actions are not conditionally revealed by HasActiveProfileIssues.'
}

Write-Host 'Launch Bar profile status polish validation passed.'
