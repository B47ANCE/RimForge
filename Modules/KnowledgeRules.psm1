Set-StrictMode -Version Latest

function Import-LoadOrderKnowledgeRules {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$TargetVersion
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [PSCustomObject]@{
            SchemaVersion = 1
            Rules         = @()
            SourcePath    = $Path
        }
    }

    try {
        $data = Get-Content `
            -LiteralPath $Path `
            -Raw |
            ConvertFrom-Json
    }
    catch {
        throw (
            "Load-order knowledge database is invalid JSON: {0}. {1}" -f
            $Path,
            $_.Exception.Message
        )
    }

    $activeRules = @(
        foreach ($rule in @($data.Rules)) {
            if (
                $rule.PSObject.Properties.Name -contains "Enabled" -and
                $rule.Enabled -eq $false
            ) {
                continue
            }

            $versions = @($rule.AppliesToVersions)

            if (
                @($versions).Count -gt 0 -and
                $versions -notcontains $TargetVersion
            ) {
                continue
            }

            $rule
        }
    )

    return [PSCustomObject]@{
        SchemaVersion = if ($null -ne $data.SchemaVersion) {
            $data.SchemaVersion
        }
        else {
            1
        }
        Rules      = @($activeRules)
        SourcePath = $Path
    }
}

Export-ModuleMember -Function Import-LoadOrderKnowledgeRules
