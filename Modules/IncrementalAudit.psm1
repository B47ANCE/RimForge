Set-StrictMode -Version Latest

function Read-RimForgeIncrementalState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { return $null }
}

function Compare-RimForgeModState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][array]$CurrentFingerprints,[AllowNull()]$PreviousState)

    $previousByKey = @{}
    if ($null -ne $PreviousState -and $PreviousState.PSObject.Properties.Name -contains 'Mods') {
        foreach ($item in @($PreviousState.Mods)) { $previousByKey[[string]$item.Key] = $item }
    }

    $currentKeys = @{}
    $changed = @(); $unchanged = @(); $added = @()
    foreach ($item in @($CurrentFingerprints)) {
        $key = [string]$item.Key; $currentKeys[$key] = $true
        if (-not $previousByKey.ContainsKey($key)) { $added += $item; $changed += $item; continue }
        if ([string]$previousByKey[$key].Fingerprint -eq [string]$item.Fingerprint) { $unchanged += $item }
        else { $changed += $item }
    }

    $removed = @()
    foreach ($key in $previousByKey.Keys) { if (-not $currentKeys.ContainsKey($key)) { $removed += $previousByKey[$key] } }

    [PSCustomObject]@{
        Total = @($CurrentFingerprints).Count
        ChangedCount = @($changed).Count
        UnchangedCount = @($unchanged).Count
        AddedCount = @($added).Count
        RemovedCount = @($removed).Count
        Changed = @($changed)
        Unchanged = @($unchanged)
        Added = @($added)
        Removed = @($removed)
        IsInitialRun = ($null -eq $PreviousState)
    }
}

function Write-RimForgeIncrementalState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][array]$Fingerprints,[AllowNull()]$RunSummary)
    $folder = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $payload = [ordered]@{
        SchemaVersion = 1
        WrittenUtc = [DateTime]::UtcNow.ToString('o')
        Mods = @($Fingerprints)
        LastRun = $RunSummary
    }
    $temp = $Path + '.tmp-' + [guid]::NewGuid().ToString('N')
    try {
        $payload | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $temp -Encoding UTF8
        Move-Item -LiteralPath $temp -Destination $Path -Force
    }
    finally { Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue }
    return [PSCustomObject]$payload
}

function New-RimForgeTimingSession {
    [CmdletBinding()]
    param()
    [PSCustomObject]@{ StartedUtc=[DateTime]::UtcNow; Stopwatch=[Diagnostics.Stopwatch]::StartNew(); Stages=[ordered]@{}; Active=@{} }
}

function Start-RimForgeTimingStage {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Session,[Parameter(Mandatory)][string]$Name)
    $Session.Active[$Name] = [Diagnostics.Stopwatch]::StartNew()
}

function Stop-RimForgeTimingStage {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Session,[Parameter(Mandatory)][string]$Name)
    if (-not $Session.Active.ContainsKey($Name)) { return 0 }
    $watch = $Session.Active[$Name]; $watch.Stop(); $Session.Active.Remove($Name)
    $milliseconds = [math]::Round($watch.Elapsed.TotalMilliseconds,2)
    $Session.Stages[$Name] = $milliseconds
    return $milliseconds
}

function Complete-RimForgeTimingSession {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Session)
    $Session.Stopwatch.Stop()
    [PSCustomObject]@{
        StartedUtc = $Session.StartedUtc.ToString('o')
        CompletedUtc = [DateTime]::UtcNow.ToString('o')
        TotalMilliseconds = [math]::Round($Session.Stopwatch.Elapsed.TotalMilliseconds,2)
        Stages = [PSCustomObject]$Session.Stages
    }
}

Export-ModuleMember -Function Read-RimForgeIncrementalState,Compare-RimForgeModState,Write-RimForgeIncrementalState,New-RimForgeTimingSession,Start-RimForgeTimingStage,Stop-RimForgeTimingStage,Complete-RimForgeTimingSession
