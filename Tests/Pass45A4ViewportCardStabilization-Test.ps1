$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$mainXaml = Get-Content (Join-Path $root 'src/RimForge.App/MainWindow.xaml') -Raw
$mainCode = Get-Content (Join-Path $root 'src/RimForge.App/MainWindow.xaml.cs') -Raw
$sorterXaml = Get-Content (Join-Path $root 'src/RimForge.App/Features/ModSorter/ModSorterView.xaml') -Raw
$searchCode = Get-Content (Join-Path $root 'src/RimForge.App/Features/Search/MainWindow.Search.cs') -Raw

$checks = @(
    @{ Name = 'Sorter statistics moved to unified workspace'; Pass = $mainXaml -match 'x:Name="ModSorterStatisticsBar"' },
    @{ Name = 'Statistics precede Issue Viewer'; Pass = $mainXaml.IndexOf('x:Name="ModSorterStatisticsBar"') -lt $mainXaml.IndexOf('x:Name="IssueViewerWorkspacePanel"') },
    @{ Name = 'Sorter view no longer owns top statistics bar'; Pass = $sorterXaml -notmatch 'Text="INSTALLED"' },
    @{ Name = 'ForgeView is hosted by a Card border'; Pass = $mainXaml -match '<Border x:Name="ForgeViewPanel" Style="\{StaticResource Card\}"' },
    @{ Name = 'Engineering Metrics follow ForgeView'; Pass = $mainXaml.IndexOf('x:Name="ForgeViewPanel"') -lt $mainXaml.IndexOf('x:Name="WorkspaceMetricsPanel"') },
    @{ Name = 'Sorter viewport receives explicit height'; Pass = $mainCode -match 'ModSorterWorkspacePanel\.Height = sorterViewportHeight' },
    @{ Name = 'Sorter viewport is capped to exact viewport'; Pass = $mainCode -match 'var sorterViewportHeight = viewportHeight' -and $mainCode -match 'ModSorterWorkspacePanel\.MaxHeight = sorterViewportHeight' },
    @{ Name = 'Sorter feature is capped to exact viewport'; Pass = $mainCode -match 'ModSorterFeature\.Height = sorterViewportHeight' -and $mainCode -match 'ModSorterFeature\.MaxHeight = sorterViewportHeight' },
    @{ Name = 'Both list hosts permit star-row shrinking'; Pass = ([regex]::Matches($sorterXaml, 'MinHeight="0" Background=').Count -ge 2) },
    @{ Name = 'Sorter view clips overflow to viewport'; Pass = $sorterXaml -match 'x:Name="ModListsViewport"[^>]*ClipToBounds="True"' },
    @{ Name = 'Engineering Metrics belongs to ForgeView'; Pass = $mainCode -match '\(WorkspaceMetricsPanel, "ForgeView", "Engineering Metrics", "ForgeView"\)' },
    @{ Name = 'Search switch uses qualified FrameworkElement'; Pass = $searchCode -match 'System\.Windows\.FrameworkElement section = destination switch' }
)

$failed = $checks | Where-Object { -not $_.Pass }
if ($failed) {
    $failed | ForEach-Object { Write-Error ("FAILED: " + $_.Name) }
    exit 1
}

Write-Host 'Pass 45A.4 viewport/card stabilization validation passed.' -ForegroundColor Green
