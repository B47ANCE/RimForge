$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$violations = Get-ChildItem (Join-Path $repo 'src') -Recurse -Filter *.xaml | Where-Object {
    (Get-Content $_.FullName -Raw) -match 'BasedOn\s*=\s*"\{DynamicResource'
}
if ($violations) {
    $paths = ($violations.FullName -join [Environment]::NewLine)
    throw "DynamicResource cannot be used for Style.BasedOn. Runtime startup failure risk:`n$paths"
}
Write-Host 'Style BasedOn runtime-safety validation passed.'
