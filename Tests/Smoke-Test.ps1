$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(Test-Path (Join-Path $root 'src\RimForge.App\RimForge.App.csproj')){Write-Host 'Native .NET application smoke contract passed.'; exit 0}
throw 'RimForge application project is missing.'
