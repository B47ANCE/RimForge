$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$view = Get-Content (Join-Path $root 'src\RimForge.App\Features\ModSorter\ModSorterView.xaml') -Raw
$viewCode = Get-Content (Join-Path $root 'src\RimForge.App\Features\ModSorter\ModSorterView.xaml.cs') -Raw
$modSorterHost = Get-Content (Join-Path $root 'src\RimForge.App\Features\ModSorter\MainWindow.ModSorter.cs') -Raw
$model = Get-Content (Join-Path $root 'src\RimForge.UI\ViewModels\ProfileLoadOrderItemViewModel.cs') -Raw
$adorner = Get-Content (Join-Path $root 'src\RimForge.App\Features\ModSorter\ModSorterDragAdorners.cs') -Raw

@('SelectionMode="Extended"','PreviewDragOver="List_PreviewDragOver"','QueryContinueDrag="List_QueryContinueDrag"','Binding IsDragGhost') | ForEach-Object {
    if (-not $view.Contains($_)) { throw "Missing direct-manipulation XAML token: $_" }
}
@('ModDragPayload','BeginDragVisual','GetInsertionIndex','ModDropInsertionAdorner','EscapePressed') | ForEach-Object {
    if (-not $viewCode.Contains($_)) { throw "Missing drag presentation token: $_" }
}
@('SelectedItems.Cast<ProfileLoadOrderItemViewModel>()','ResolveDependencyAssistanceForGroup','RegisterLoadOrderUndo','Drop rejected','Mod group moved') | ForEach-Object {
    if (-not $modSorterHost.Contains($_)) { throw "Missing group drag workflow token: $_" }
}
if (-not $model.Contains('public bool IsDragGhost')) { throw 'Drag ghost state is missing from the load-order item model.' }
@('ModDragPreviewAdorner','ModDropInsertionAdorner','DrawLine') | ForEach-Object {
    if (-not $adorner.Contains($_)) { throw "Missing drag adorner token: $_" }
}
Write-Host 'Mod Library direct manipulation validation passed.'
