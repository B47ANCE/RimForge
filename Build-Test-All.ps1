[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $SkipPowerShellTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $root 'RimForge.sln'

Push-Location $root
try {
    dotnet restore $solution
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build $solution --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project .\tests\RimForge.ExecutionTests\RimForge.ExecutionTests.csproj `
        --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (-not $SkipPowerShellTests) {
        & .\tests\Run-AllTests.ps1 -Configuration $Configuration -NoBuild -SkipStartupSmoke
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

}
finally {
    Pop-Location
}

Write-Host 'RimForge validation completed successfully.' -ForegroundColor Green
