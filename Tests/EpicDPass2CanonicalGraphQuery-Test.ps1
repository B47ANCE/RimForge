$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$models = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Models/ForgeGraphQueryModels.cs')
$service = Get-Content -Raw (Join-Path $root 'src/RimForge.Core/Services/ForgeGraphQueryService.cs')
$canvas = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Features/ForgeView/ForgeGraphCanvas.cs')
$view = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Features/ForgeView/ForgeViewView.xaml.cs')
$search = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Features/Search/MainWindow.Search.cs')
$issues = Get-Content -Raw (Join-Path $root 'src/RimForge.App/Features/IssueViewer/MainWindow.IssueViewer.cs')
$projection = Get-Content -Raw (Join-Path $root 'src/RimForge.Infrastructure/Services/ForgeGraphProjectionService.cs')

foreach ($token in @('ForgeGraphQuery', 'ForgeGraphQueryResult', 'ForgeGraphSelectionSnapshot', 'ForgeGraphRelationshipProvenance', 'ForgeGraphQueryOrigin')) {
    if ($models -notmatch "record $token|enum $token") { throw "Core canonical Forge graph model is missing: $token" }
}
if ($service -notmatch 'interface IForgeGraphQueryService' -or $service -notmatch 'class ForgeGraphQueryService') { throw 'Canonical Forge graph query service is missing.' }
if ($service -notmatch 'class ForgeGraphSelectionState') { throw 'Canonical Forge graph selection state is missing.' }
if ($canvas -notmatch 'QueryService\.Execute') { throw 'Canvas does not consume the canonical graph query.' }
if ($view -notmatch 'QueryService\.Execute' -or $view -notmatch 'BuildCurrentQuery') { throw 'Outline does not consume the canonical graph query.' }
if ($search -notmatch 'ForgeGraphQueryOrigin\.Search') { throw 'Search navigation does not publish its canonical graph selection origin.' }
if ($issues -notmatch 'ForgeGraphQueryOrigin\.IssueNavigation') { throw 'Issue navigation does not publish its canonical graph selection origin.' }
if ($view -notmatch 'SelectionHistory' -or $view -notmatch 'FocusedPackageId' -or $view -notmatch 'SelectedPackageId') { throw 'Profile-owned Forge selection and focus state is not persisted.' }
if ($projection -notmatch 'ForgeGraphRelationshipProvenance') { throw 'Projected relationships do not carry evidence provenance.' }

Write-Host 'Epic D Pass 2 canonical graph query and selection verified.'
