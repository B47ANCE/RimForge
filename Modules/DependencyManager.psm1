Set-StrictMode -Version Latest

function Import-RimForgeDependencyManifest {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Dependency manifest not found: $Path" }
    try { $manifest = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { throw "Dependency manifest JSON is invalid: $($_.Exception.Message)" }
    if (-not ($manifest.PSObject.Properties.Name -contains 'Dependencies')) { throw 'Dependency manifest is missing Dependencies.' }
    return $manifest
}

function Resolve-RimForgeDependencyCandidatePaths {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ProjectRoot,
        [Parameter(Mandatory)]$Dependency
    )
    $items = @()
    if ($Dependency.PSObject.Properties.Name -contains 'ConfiguredPath' -and -not [string]::IsNullOrWhiteSpace([string]$Dependency.ConfiguredPath)) {
        $path = [string]$Dependency.ConfiguredPath
        if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $ProjectRoot $path }
        $items += [PSCustomObject]@{ Source = 'Configured'; Path = [IO.Path]::GetFullPath($path) }
    }
    if ($Dependency.PSObject.Properties.Name -contains 'SearchCommand' -and -not [string]::IsNullOrWhiteSpace([string]$Dependency.SearchCommand)) {
        $command = Get-Command ([string]$Dependency.SearchCommand) -ErrorAction SilentlyContinue
        if ($null -ne $command) { $items += [PSCustomObject]@{ Source = 'PATH'; Path = [string]$command.Source } }
    }
    $seen = @{}
    return @($items | Where-Object { $key = ([string]$_.Path).ToLowerInvariant(); if ($seen.ContainsKey($key)) { $false } else { $seen[$key] = $true; $true } })
}

function Test-RimForgeDependency {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ProjectRoot,
        [Parameter(Mandatory)]$Dependency,
        [int]$TimeoutSeconds = 10
    )
    $invalid = @()
    foreach ($candidate in @(Resolve-RimForgeDependencyCandidatePaths -ProjectRoot $ProjectRoot -Dependency $Dependency)) {
        if (-not (Test-Path -LiteralPath $candidate.Path -PathType Leaf)) { continue }
        try {
            switch ([string]$Dependency.Validator) {
                'Texconv' {
                    $result = Test-RimForgeTexconvExecutable -Path $candidate.Path -TimeoutSeconds $TimeoutSeconds
                    if ($result.IsValid) {
                        return [PSCustomObject]@{ Id=[string]$Dependency.Id; DisplayName=[string]$Dependency.DisplayName; Capability=[string]$Dependency.Capability; Required=[bool]$Dependency.Required; IsInstalled=$true; IsValid=$true; Source=$candidate.Source; Path=$candidate.Path; Architecture=$result.Architecture; VersionText=$result.VersionText; Error=$null; CanInstall=(Test-RimForgeDependencyCanInstall -Dependency $Dependency); InvalidCandidates=@($invalid) }
                    }
                    $invalid += [PSCustomObject]@{ Source=$candidate.Source; Path=$candidate.Path; Error=$result.Error }
                }
                default { throw "Unknown dependency validator '$($Dependency.Validator)' for '$($Dependency.Id)'." }
            }
        }
        catch { $invalid += [PSCustomObject]@{ Source=$candidate.Source; Path=$candidate.Path; Error=$_.Exception.Message } }
    }
    $first = @($invalid | Select-Object -First 1)
    return [PSCustomObject]@{ Id=[string]$Dependency.Id; DisplayName=[string]$Dependency.DisplayName; Capability=[string]$Dependency.Capability; Required=[bool]$Dependency.Required; IsInstalled=(@($invalid).Count -gt 0); IsValid=$false; Source=if($first.Count){$first[0].Source}else{$null}; Path=if($first.Count){$first[0].Path}else{$null}; Architecture=$null; VersionText=$null; Error=if($first.Count){$first[0].Error}else{'Dependency was not found.'}; CanInstall=(Test-RimForgeDependencyCanInstall -Dependency $Dependency); InvalidCandidates=@($invalid) }
}

function Test-RimForgeDependencyCanInstall {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Dependency)
    if (-not ($Dependency.PSObject.Properties.Name -contains 'Installer') -or $null -eq $Dependency.Installer) { return $false }
    switch ([string]$Dependency.Installer.Type) { 'Winget' { return ($null -ne (Get-Command 'winget.exe' -ErrorAction SilentlyContinue)) } default { return $false } }
}

function Get-RimForgeDependencyStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ProjectRoot,
        [Parameter(Mandatory)]$Manifest,
        [AllowNull()][string]$Id,
        [AllowNull()][string]$Capability,
        [int]$TimeoutSeconds = 10
    )
    $dependencies = @($Manifest.Dependencies)
    if (-not [string]::IsNullOrWhiteSpace($Id)) { $dependencies = @($dependencies | Where-Object { [string]$_.Id -eq $Id }) }
    if (-not [string]::IsNullOrWhiteSpace($Capability)) { $dependencies = @($dependencies | Where-Object { [string]$_.Capability -eq $Capability }) }
    return @($dependencies | ForEach-Object { Test-RimForgeDependency -ProjectRoot $ProjectRoot -Dependency $_ -TimeoutSeconds $TimeoutSeconds })
}

function Request-RimForgeDependencyInstallApproval {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Dependency,[AllowNull()][string]$Reason)
    if ([string]$Dependency.Id -eq 'texconv' -and (Get-Command Request-RimForgeTexconvInstallApproval -ErrorAction SilentlyContinue)) {
        return Request-RimForgeTexconvInstallApproval -Reason $(if([string]::IsNullOrWhiteSpace($Reason)){"$($Dependency.DisplayName) is required."}else{$Reason})
    }
    return $false
}

function Install-RimForgeDependency {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)]$Dependency,[switch]$AcceptPackageAgreements)
    if (-not (Test-RimForgeDependencyCanInstall -Dependency $Dependency)) { throw "No supported installer is available for '$($Dependency.Id)'." }
    switch ([string]$Dependency.Installer.Type) {
        'Winget' {
            $winget = Get-Command 'winget.exe' -ErrorAction Stop
            $args = @('install','--id',[string]$Dependency.Installer.PackageId)
            if ([bool]$Dependency.Installer.Exact) { $args += '--exact' }
            if ($AcceptPackageAgreements) { $args += @('--accept-package-agreements','--accept-source-agreements') }
            if ($PSCmdlet.ShouldProcess([string]$Dependency.DisplayName,'Install with winget')) {
                $process = Start-Process -FilePath $winget.Source -ArgumentList $args -Wait -PassThru -NoNewWindow
                if ($process.ExitCode -ne 0) { throw "winget exited with code $($process.ExitCode)." }
            }
        }
        default { throw "Unsupported installer type '$($Dependency.Installer.Type)'." }
    }
}

function Ensure-RimForgeDependency {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ProjectRoot,
        [Parameter(Mandatory)]$Dependency,
        [int]$TimeoutSeconds = 10,
        [switch]$PromptForInstall,
        [switch]$AcceptPackageAgreements
    )
    $status = Test-RimForgeDependency -ProjectRoot $ProjectRoot -Dependency $Dependency -TimeoutSeconds $TimeoutSeconds
    if ($status.IsValid) { return $status }
    if (-not $PromptForInstall -or -not $status.CanInstall) { return $status }
    if (-not (Request-RimForgeDependencyInstallApproval -Dependency $Dependency -Reason "$($Dependency.DisplayName) is missing or invalid. $($status.Error)")) { return $status }
    Install-RimForgeDependency -Dependency $Dependency -AcceptPackageAgreements:$AcceptPackageAgreements -Confirm:$false
    return Test-RimForgeDependency -ProjectRoot $ProjectRoot -Dependency $Dependency -TimeoutSeconds $TimeoutSeconds
}

Export-ModuleMember -Function Import-RimForgeDependencyManifest,Resolve-RimForgeDependencyCandidatePaths,Test-RimForgeDependencyCanInstall,Test-RimForgeDependency,Get-RimForgeDependencyStatus,Request-RimForgeDependencyInstallApproval,Install-RimForgeDependency,Ensure-RimForgeDependency
