$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$forbiddenDirectoryNames = @('bin', 'obj', '.vs', '.patch-backups', 'TestResults')
$forbiddenPathPattern = '(^|/)(bin|obj|\.vs|\.patch-backups|TestResults)(/|$)'

# Generated directories are expected after a local build. Hygiene means they must
# be ignored and must never be tracked by Git, not that they cannot exist locally.
$gitCommand = Get-Command git -ErrorAction SilentlyContinue
$gitDirectory = Join-Path $root '.git'
if ($gitCommand -and (Test-Path (Join-Path $gitDirectory 'HEAD'))) {
    $isWorkTree = & git -C $root rev-parse --is-inside-work-tree 2>$null
    if ($LASTEXITCODE -eq 0 -and $isWorkTree -eq 'true') {
        $trackedPaths = @(& git -C $root ls-files 2>$null)

        $trackedGeneratedPaths = @($trackedPaths | Where-Object {
            ($_.Replace('\', '/')) -match $forbiddenPathPattern
        })

        if ($trackedGeneratedPaths.Count -gt 0) {
            throw "Generated or local-only paths are tracked by Git: $($trackedGeneratedPaths -join ', ')"
        }

        $sourceFiles = @(Get-ChildItem (Join-Path $root 'src') -Recurse -File | Where-Object {
            $_.FullName -notmatch '(?i)[\\/](bin|obj)[\\/]'
        })
        $ignoredSourceFiles = @($sourceFiles | Where-Object {
            & git -C $root check-ignore --quiet -- $_.FullName
            $LASTEXITCODE -eq 0
        })
        if ($ignoredSourceFiles.Count -gt 0) {
            throw "Source files are hidden by .gitignore rules: $($ignoredSourceFiles.FullName -join ', ')"
        }
    }
}

$forbiddenFiles = Get-ChildItem $root -Recurse -File -Force | Where-Object {
    $_.FullName -notmatch '(?i)[\\/](bin|obj|\.vs|\.patch-backups|TestResults)[\\/]' -and
    (
        $_.Name -match '(?i)(\.bak$|\.backup$|\.old$|\.tmp$|\.user$|\.suo$|copy of|deprecated|rejected)' -or
        $_.FullName -match '(?i)[\\/]Assets[\\/].*[\\/](Concept|Deprecated|Rejected)[\\/]'
    )
}
if ($forbiddenFiles) {
    throw "Repository hygiene violation: $($forbiddenFiles.FullName -join ', ')"
}

$gitIgnorePath = Join-Path $root '.gitignore'
if (-not (Test-Path $gitIgnorePath)) {
    throw 'Repository root is missing .gitignore.'
}

$gitIgnore = Get-Content $gitIgnorePath -Raw
foreach ($requiredRule in @('**/bin/', '**/obj/', 'Output/', 'Logs/', 'Cache/', 'Temp/', 'Exports/', '.patch-backups/')) {
    if ($gitIgnore -notmatch [regex]::Escape($requiredRule)) {
        throw ".gitignore is missing required rule: $requiredRule"
    }
}

Write-Host 'Repository hygiene validation passed.' -ForegroundColor Green
