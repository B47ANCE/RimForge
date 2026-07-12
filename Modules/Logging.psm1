Set-StrictMode -Version Latest

$script:LogFile = $null

function Initialize-Logger {
    param(
        [Parameter(Mandatory)]
        [string]$LogDirectory
    )

    if (-not (Test-Path $LogDirectory)) {
        New-Item -ItemType Directory -Path $LogDirectory | Out-Null
    }

    $timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
    $script:LogFile = Join-Path $LogDirectory "Audit_$timestamp.log"

    New-Item -ItemType File -Path $script:LogFile -Force | Out-Null
}

function Write-Log {
    param(
        [ValidateSet("INFO","WARNING","ERROR","SUCCESS","DEBUG")]
        [string]$Level = "INFO",

        [Parameter(Mandatory)]
        [string]$Message
    )

    $time = Get-Date -Format "HH:mm:ss"

    $line = "[$time][$Level] $Message"

    switch ($Level) {
        "INFO"    { $color = "White" }
        "WARNING" { $color = "Yellow" }
        "ERROR"   { $color = "Red" }
        "SUCCESS" { $color = "Green" }
        "DEBUG"   { $color = "DarkGray" }
    }

    Write-Host $line -ForegroundColor $color

    if ($script:LogFile) {
        Add-Content -Path $script:LogFile -Value $line
    }
}

Export-ModuleMember -Function Initialize-Logger, Write-Log