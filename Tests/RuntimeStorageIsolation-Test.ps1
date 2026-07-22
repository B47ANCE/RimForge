$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Assert-FileContains([string]$Path, [string]$Pattern, [string]$Message) {
    $content = Get-Content (Join-Path $root $Path) -Raw
    if ($content -notmatch $Pattern) { throw $Message }
}

Assert-FileContains '.gitignore' '(?m)^/Output/\r?$' '.gitignore must ignore only the root legacy Output runtime data.'
Assert-FileContains '.gitignore' '(?m)^/Logs/\r?$' '.gitignore must ignore only the root legacy Logs runtime data.'
Assert-FileContains '.gitignore' '(?m)^/Cache/\r?$' '.gitignore must ignore only the root legacy Cache runtime data.'
Assert-FileContains '.gitignore' '(?m)^/Temp/\r?$' '.gitignore must ignore only the root legacy Temp runtime data.'
Assert-FileContains '.gitignore' '(?m)^/Exports/\r?$' '.gitignore must ignore only the root legacy Exports runtime data.'
Assert-FileContains 'src/RimForge.Core/Models/RimForgePaths.cs' 'Environment\.SpecialFolder\.LocalApplicationData' 'Runtime paths must use LocalApplicationData.'
Assert-FileContains 'src/RimForge.Core/Models/RimForgePaths.cs' 'Path\.Combine\(localAppData, "RimForge"\)' 'Runtime data must be scoped beneath the RimForge application directory.'
Assert-FileContains 'src/RimForge.Core/Models/RimForgePaths.cs' 'Path\.Combine\(applicationDataRoot, configuredOutput\)' 'Relative output paths must resolve outside the repository.'
Assert-FileContains 'src/RimForge.App/MainWindow.xaml.cs' 'RuntimePaths\.ProfilesRoot' 'Profile runtime state must use the centralized runtime layout.'

Write-Host 'Runtime storage isolation validation passed.' -ForegroundColor Green
