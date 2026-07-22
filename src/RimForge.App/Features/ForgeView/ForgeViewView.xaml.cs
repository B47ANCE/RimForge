using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Threading;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.App.Features.ForgeView;

public sealed class ModNavigationRequestedEventArgs : EventArgs
{
    public ModNavigationRequestedEventArgs(string packageId) => PackageId = packageId;
    public string PackageId { get; }
}

public partial class ForgeViewView : UserControl
{
    private static readonly IForgeGraphQueryService QueryService = new ForgeGraphQueryService();
    private readonly ForgeGraphSelectionState _selectionState = new();
    private MainWindow? _subscribedWindow;
    private string? _loadedLayoutWorkspace;
    private readonly DispatcherTimer _layoutSaveTimer;
    public static readonly RoutedEvent RefreshRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(RefreshRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ForgeViewView));

    public ForgeViewView()
    {
        InitializeComponent();
        _layoutSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _layoutSaveTimer.Tick += (_, _) => { _layoutSaveTimer.Stop(); SaveProfileLayout(); };
        GraphViewport.AddHandler(
            Mouse.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(GraphViewport_PreviewMouseWheel),
            handledEventsToo: true);
        Loaded += (_, _) => { SubscribeToWindow(); LoadProfileLayout(force: true); RebuildOutline(); };
        DataContextChanged += (_, _) => { SubscribeToWindow(); LoadProfileLayout(force: true); RebuildOutline(); };
        Unloaded += (_, _) => UnsubscribeFromWindow();
    }

    public event EventHandler<ModNavigationRequestedEventArgs>? ModNavigationRequested;
    public event RoutedEventHandler RefreshRequested { add => AddHandler(RefreshRequestedEvent, value); remove => RemoveHandler(RefreshRequestedEvent, value); }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(RefreshRequestedEvent, this));
    private void GraphMode_Click(object sender, RoutedEventArgs e)
    {
        GraphCanvas.Visibility = Visibility.Visible;
        OutlineScroller.Visibility = Visibility.Collapsed;
        GraphModeButton.Tag = "Active";
        OutlineModeButton.Tag = null;
    }
    private void OutlineMode_Click(object sender, RoutedEventArgs e)
    {
        RebuildOutline();
        GraphCanvas.Visibility = Visibility.Collapsed;
        OutlineScroller.Visibility = Visibility.Visible;
        GraphModeButton.Tag = null;
        OutlineModeButton.Tag = "Active";
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => GraphCanvas.ZoomIn();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => GraphCanvas.ZoomOut();
    private void ResetView_Click(object sender, RoutedEventArgs e) => GraphCanvas.FitToView();
    private void CenterSelection_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindow window) GraphCanvas.CenterOnPackage(window.SelectedMod?.PackageId);
    }
    private void PathFocus_Click(object sender, RoutedEventArgs e)
    {
        GraphCanvas.HighlightDependencyPaths = !GraphCanvas.HighlightDependencyPaths;
        PathFocusButton.Content = GraphCanvas.HighlightDependencyPaths ? "Paths: On" : "Paths: Off";
    }
    private void Minimap_Click(object sender, RoutedEventArgs e)
    {
        GraphCanvas.ShowMinimap = !GraphCanvas.ShowMinimap;
        MinimapButton.Content = GraphCanvas.ShowMinimap ? "Minimap: On" : "Minimap: Off";
    }
    private void IsolatePath_Click(object sender, RoutedEventArgs e)
    {
        GraphCanvas.IsolateFocusedPath = !GraphCanvas.IsolateFocusedPath;
        IsolatePathButton.Content = GraphCanvas.IsolateFocusedPath ? "Isolate Path: On" : "Isolate Path: Off";
        IsolatePathButton.Tag = GraphCanvas.IsolateFocusedPath ? "Active" : null;
        RebuildOutline();
        RequestLayoutSave();
    }

    private void GraphFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GraphCanvas is null || HealthFilterCombo is null || RelationshipFilterCombo is null) return;
        GraphCanvas.HealthFilter = SelectedTag(HealthFilterCombo, "All");
        GraphCanvas.RelationshipFilter = SelectedTag(RelationshipFilterCombo, "All");
        RebuildOutline();
        RequestLayoutSave();
    }

    private static string SelectedTag(ComboBox comboBox, string fallback) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;

    private static void SelectTag(ComboBox comboBox, string? value)
    {
        var desired = string.IsNullOrWhiteSpace(value) ? "All" : value;
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (!string.Equals(item.Tag?.ToString(), desired, StringComparison.OrdinalIgnoreCase)) continue;
            comboBox.SelectedItem = item;
            return;
        }
        comboBox.SelectedIndex = 0;
    }

    private void EditLayout_Click(object sender, RoutedEventArgs e)
    {
        GraphCanvas.IsLayoutEditMode = !GraphCanvas.IsLayoutEditMode;
        EditLayoutButton.Content = GraphCanvas.IsLayoutEditMode ? "Edit Layout: On" : "Edit Layout: Off";
        EditLayoutButton.Tag = GraphCanvas.IsLayoutEditMode ? "Active" : null;
    }
    private void PinNode_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindow window || string.IsNullOrWhiteSpace(window.SelectedMod?.PackageId)) return;
        var pinned = GraphCanvas.TogglePin(window.SelectedMod.PackageId);
        PinNodeButton.Content = pinned ? "Unpin Node" : "Pin Node";
        SaveProfileLayout();
    }
    private void ResetLayout_Click(object sender, RoutedEventArgs e)
    {
        GraphCanvas.ResetCustomLayout();
        SaveProfileLayout();
        PinNodeButton.Content = "Pin Node";
    }
    private void GraphCanvas_ViewChanged(object? sender, EventArgs e)
    {
        ZoomText.Text = $"{GraphCanvas.Zoom:P0}";
        RequestLayoutSave();
    }

    public bool OwnsGraphWheelInput(DependencyObject? source)
    {
        if (GraphCanvas.Visibility != Visibility.Visible || source is null) return false;
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (ReferenceEquals(current, GraphViewport)) return true;
            if (ReferenceEquals(current, this)) break;
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is System.Windows.Media.Visual || current is System.Windows.Media.Media3D.Visual3D)
            return System.Windows.Media.VisualTreeHelper.GetParent(current);

        return LogicalTreeHelper.GetParent(current);
    }

    private void GraphViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (GraphCanvas.Visibility != Visibility.Visible || e.Delta == 0) return;
        GraphCanvas.ZoomAt(e.Delta, e.GetPosition(GraphCanvas));
        e.Handled = true;
    }
    private void GraphCanvas_NodeInvoked(object? sender, ForgeGraphNodeInvokedEventArgs e)
    {
        RecordSelection(e.PackageId);
        if (DataContext is MainWindow window) window.SetForgeInteractionSelection(e.PackageId);
        ModNavigationRequested?.Invoke(this, new ModNavigationRequestedEventArgs(e.PackageId));
    }

    private void GraphCanvas_NodeHoverChanged(object? sender, ForgeGraphNodeHoverChangedEventArgs e)
    {
        if (DataContext is MainWindow window) window.SetForgeHoveredPackage(e.PackageId);
    }

    private void GraphCanvas_RenderCompleted(object? sender, ForgeGraphRenderCompletedEventArgs e)
    {
        FilterStatusText.Text = $"Showing {e.TotalNodes:N0} mods • {e.TotalEdges:N0} relationships";
        if (DataContext is MainWindow window) window.SetForgeGraphRenderMetrics(e);
    }

    private void GraphCanvas_LayoutChanged(object? sender, ForgeGraphLayoutChangedEventArgs e)
    {
        PinNodeButton.Content = e.IsPinned ? "Unpin Node" : "Pin Node";
        RequestLayoutSave();
    }

    private void RequestLayoutSave()
    {
        _layoutSaveTimer.Stop();
        _layoutSaveTimer.Start();
    }

    private void SubscribeToWindow()
    {
        UnsubscribeFromWindow();
        if (DataContext is not MainWindow window) return;
        _subscribedWindow = window;
        window.PropertyChanged += Window_PropertyChanged;
    }

    private void UnsubscribeFromWindow()
    {
        if (_subscribedWindow is not null) _subscribedWindow.PropertyChanged -= Window_PropertyChanged;
        _subscribedWindow = null;
    }

    private void Window_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindow.SelectedProfile)) { LoadProfileLayout(force: false); RebuildOutline(); }
        if (e.PropertyName == nameof(MainWindow.ShowFullLibrary)) RebuildOutline();
        if (e.PropertyName is nameof(MainWindow.SearchText) or nameof(MainWindow.SearchMatchedPackageIds)) RebuildOutline();
        if (e.PropertyName == nameof(MainWindow.SelectedMod) && DataContext is MainWindow window)
        {
            PinNodeButton.Content = GraphCanvas.PinnedPackageIds.Contains(window.SelectedMod?.PackageId ?? string.Empty, StringComparer.OrdinalIgnoreCase) ? "Unpin Node" : "Pin Node";
            SynchronizeSelection(window.SelectedMod?.PackageId, ForgeGraphQueryOrigin.Inspector);
        }
    }

    private void LoadProfileLayout(bool force)
    {
        if (DataContext is not MainWindow window || window.SelectedProfile is null) return;
        var workspace = window.SelectedProfile.WorkspacePath;
        if (!force && string.Equals(workspace, _loadedLayoutWorkspace, StringComparison.OrdinalIgnoreCase)) return;
        _loadedLayoutWorkspace = workspace;
        var path = Path.Combine(workspace, "ForgeView.layout.json");
        if (!File.Exists(path))
        {
            GraphCanvas.ResetCustomLayout();
            _selectionState.Restore(null, -1, window.SelectedMod?.PackageId, window.SelectedMod?.PackageId);
            UpdateSelectionHistoryButtons();
            return;
        }
        try
        {
            var state = JsonSerializer.Deserialize<ForgeLayoutDocument>(File.ReadAllText(path));
            var positions = state?.Nodes?.ToDictionary(pair => pair.Key, pair => new Point(pair.Value.X, pair.Value.Y), StringComparer.OrdinalIgnoreCase);
            GraphCanvas.ApplyLayoutState(positions, state?.PinnedPackageIds, state?.Zoom ?? 1, new Point(state?.PanX ?? 24, state?.PanY ?? 24));
            SelectTag(HealthFilterCombo, state?.HealthFilter);
            SelectTag(RelationshipFilterCombo, state?.RelationshipFilter);
            GraphCanvas.HealthFilter = state?.HealthFilter ?? "All";
            GraphCanvas.RelationshipFilter = state?.RelationshipFilter ?? "All";
            GraphCanvas.IsolateFocusedPath = state?.IsolateFocusedPath ?? false;
            IsolatePathButton.Content = GraphCanvas.IsolateFocusedPath ? "Isolate Path: On" : "Isolate Path: Off";
            IsolatePathButton.Tag = GraphCanvas.IsolateFocusedPath ? "Active" : null;
            _selectionState.Restore(state?.SelectionHistory, state?.SelectionHistoryIndex ?? -1, state?.SelectedPackageId ?? window.SelectedMod?.PackageId, state?.FocusedPackageId);
            UpdateSelectionHistoryButtons();
            if (!string.IsNullOrWhiteSpace(state?.SelectedPackageId) &&
                !string.Equals(state.SelectedPackageId, window.SelectedMod?.PackageId, StringComparison.OrdinalIgnoreCase))
                Dispatcher.BeginInvoke(() => ModNavigationRequested?.Invoke(this, new ModNavigationRequestedEventArgs(state.SelectedPackageId)));
        }
        catch
        {
            GraphCanvas.ResetCustomLayout();
        }
    }

    private void SaveProfileLayout()
    {
        if (DataContext is not MainWindow window || window.SelectedProfile is null || window.SelectedProfile.IsLocked) return;
        var path = Path.Combine(window.SelectedProfile.WorkspacePath, "ForgeView.layout.json");
        try
        {
            Directory.CreateDirectory(window.SelectedProfile.WorkspacePath);
            var state = new ForgeLayoutDocument(
                GraphCanvas.CustomPositions.ToDictionary(pair => pair.Key, pair => new ForgeLayoutPoint(pair.Value.X, pair.Value.Y), StringComparer.OrdinalIgnoreCase),
                GraphCanvas.PinnedPackageIds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                GraphCanvas.Zoom,
                GraphCanvas.Pan.X,
                GraphCanvas.Pan.Y,
                GraphCanvas.HealthFilter,
                GraphCanvas.RelationshipFilter,
                GraphCanvas.IsolateFocusedPath,
                _selectionState.Current.History,
                _selectionState.Current.HistoryIndex,
                _selectionState.Current.FocusedPackageId,
                _selectionState.Current.SelectedPackageId);
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, path, true);
        }
        catch
        {
            // Layout persistence is best-effort and must not interrupt graph interaction.
        }
    }

    private sealed record ForgeLayoutPoint(double X, double Y);
    private sealed record ForgeLayoutDocument(
        Dictionary<string, ForgeLayoutPoint> Nodes,
        IReadOnlyList<string> PinnedPackageIds,
        double Zoom,
        double PanX,
        double PanY,
        string HealthFilter = "All",
        string RelationshipFilter = "All",
        bool IsolateFocusedPath = false,
        IReadOnlyList<string>? SelectionHistory = null,
        int SelectionHistoryIndex = -1,
        string? FocusedPackageId = null,
        string? SelectedPackageId = null);

    private void PreviousSelection_Click(object sender, RoutedEventArgs e) => NavigateSelectionHistory(-1);
    private void NextSelection_Click(object sender, RoutedEventArgs e) => NavigateSelectionHistory(1);

    private void RecordSelection(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return;
        _selectionState.Select(packageId, ForgeGraphQueryOrigin.Canvas);
        UpdateSelectionHistoryButtons();
        RequestLayoutSave();
    }

    private void NavigateSelectionHistory(int offset)
    {
        var before = _selectionState.Current.HistoryIndex;
        var selection = _selectionState.Navigate(offset);
        if (selection.HistoryIndex == before || string.IsNullOrWhiteSpace(selection.SelectedPackageId)) return;
        ModNavigationRequested?.Invoke(this, new ModNavigationRequestedEventArgs(selection.SelectedPackageId));
        GraphCanvas.CenterOnPackage(selection.FocusedPackageId);
        UpdateSelectionHistoryButtons();
        RequestLayoutSave();
    }

    private void UpdateSelectionHistoryButtons()
    {
        PreviousSelectionButton.IsEnabled = _selectionState.Current.HistoryIndex > 0;
        NextSelectionButton.IsEnabled = _selectionState.Current.HistoryIndex >= 0 && _selectionState.Current.HistoryIndex < _selectionState.Current.History.Count - 1;
    }

    public void SynchronizeSelection(string? packageId, ForgeGraphQueryOrigin origin)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return;
        _selectionState.Select(packageId, origin);
        UpdateSelectionHistoryButtons();
        RequestLayoutSave();
    }

    private ForgeGraphQuery BuildCurrentQuery(MainWindow window)
    {
        ModHealthStatus? health = Enum.TryParse<ModHealthStatus>(SelectedTag(HealthFilterCombo, "All"), true, out var parsedHealth) ? parsedHealth : null;
        IReadOnlyCollection<DependencyRelationshipType> relationships = SelectedTag(RelationshipFilterCombo, "All") switch
        {
            "Required" => [DependencyRelationshipType.Required, DependencyRelationshipType.PatchTarget],
            "Optional" => [DependencyRelationshipType.Optional],
            "Ordering" => [DependencyRelationshipType.LoadBefore, DependencyRelationshipType.LoadAfter],
            "Conflicts" => [DependencyRelationshipType.Incompatible],
            _ => Array.Empty<DependencyRelationshipType>()
        };
        return new ForgeGraphQuery(
            window.SearchMatchedPackageIds,
            window.IsSearchActive,
            window.SelectedProfile?.ActiveMods,
            window.ShowFullLibrary,
            health,
            relationships,
            window.SelectedMod?.PackageId,
            GraphCanvas.IsolateFocusedPath);
    }

    private void RebuildOutline()
    {
        if (DataContext is not MainWindow window) return;
        var result = QueryService.Execute(
            new DependencyGraphModel(window.DependencyNodes.ToArray(), window.DependencyEdges.Where(ForgeGraphPresentationPolicy.ShouldDisplayEdge).ToArray()),
            BuildCurrentQuery(window));
        var nodes = result.Nodes.ToDictionary(node => node.PackageId ?? node.Id, StringComparer.OrdinalIgnoreCase);
        var edges = result.Edges.ToList();

        var roots = nodes.Keys.Where(id => !edges.Any(edge => string.Equals(edge.SourceId, id, StringComparison.OrdinalIgnoreCase))).OrderBy(id => nodes[id].Name).ToList();
        if (roots.Count == 0) roots = nodes.Keys.OrderBy(id => nodes[id].Name).Take(1).ToList();
        var items = new List<FrameworkElement>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < roots.Count; index++)
            AddOutlineNode(items, roots[index], 0, index == roots.Count - 1, nodes, edges, visited);

        var remaining = nodes.Keys.Where(id => !visited.Contains(id)).OrderBy(id => nodes[id].Name).ToList();
        for (var index = 0; index < remaining.Count; index++)
            AddOutlineNode(items, remaining[index], 0, index == remaining.Count - 1, nodes, edges, visited);
        OutlineItems.ItemsSource = items;
    }

    private void AddOutlineNode(ICollection<FrameworkElement> output, string id, int depth, bool isLastSibling,
        IReadOnlyDictionary<string, DependencyGraphNode> nodes, IReadOnlyList<DependencyGraphEdge> edges, ISet<string> visited)
    {
        if (!nodes.TryGetValue(id, out var node)) return;
        var repeated = !visited.Add(id);

        var row = new Grid
        {
            Margin = new Thickness(depth * 24, 2, 4, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var branch = new TextBlock
        {
            Text = depth == 0 ? "◆" : isLastSibling ? "└─" : "├─",
            Width = depth == 0 ? 22 : 30,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Left
        };
        branch.SetResourceReference(TextBlock.ForegroundProperty, depth == 0 ? "AccentBrush" : "TextMutedBrush");
        row.Children.Add(branch);

        var label = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        var name = new TextBlock
        {
            Text = node.Name,
            FontWeight = depth == 0 ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Left
        };
        var package = new TextBlock
        {
            Text = $"  [{id}]" + (repeated ? "  (linked above)" : string.Empty),
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Left
        };
        package.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        label.Children.Add(name);
        label.Children.Add(package);
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        var button = new Button
        {
            Content = row,
            Tag = id,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0),
            Padding = new Thickness(8, 5, 8, 5)
        };
        button.SetResourceReference(StyleProperty, "SecondaryButton");
        button.Click += (_, _) =>
        {
            _selectionState.Select(id, ForgeGraphQueryOrigin.Outline);
            UpdateSelectionHistoryButtons();
            RequestLayoutSave();
            ModNavigationRequested?.Invoke(this, new ModNavigationRequestedEventArgs(id));
        };
        output.Add(button);

        if (repeated || depth > 20) return;
        var children = edges
            .Where(edge => string.Equals(edge.TargetId, id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(edge => nodes.TryGetValue(edge.SourceId, out var childNode) ? childNode.Name : edge.SourceId)
            .ToList();
        for (var index = 0; index < children.Count; index++)
            AddOutlineNode(output, children[index].SourceId, depth + 1, index == children.Count - 1, nodes, edges, visited);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindow window) return;
        var dialog = new SaveFileDialog { Title = "Export ForgeView relationships", Filter = "Graphviz DOT (*.dot)|*.dot|CSV (*.csv)|*.csv", FileName = "RimForge-DependencyGraph.dot" };
        if (dialog.ShowDialog() != true) return;
        var edges = window.DependencyEdges.ToList();
        if (Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = new StringBuilder("Source,Target,Relationship,Description,ProvenanceKind,ProvenanceSource,EvidenceIds\r\n");
            foreach (var edge in edges)
            {
                var provenance = edge.Provenance ?? ForgeGraphRelationshipProvenance.FromDeclaration(edge);
                csv.AppendLine($"\"{Escape(edge.SourceId)}\",\"{Escape(edge.TargetId)}\",\"{edge.Relationship}\",\"{Escape(edge.Description)}\",\"{Escape(provenance.SourceKind)}\",\"{Escape(provenance.SourceId)}\",\"{Escape(string.Join(";", provenance.EvidenceIds))}\"");
            }
            File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
        }
        else
        {
            var dot = new StringBuilder("digraph RimForge {\r\n  rankdir=LR;\r\n  node [shape=box, style=rounded];\r\n");
            foreach (var node in window.DependencyNodes) dot.AppendLine($"  \"{Escape(node.PackageId ?? node.Id)}\" [label=\"{Escape(node.Name)}\"];");
            foreach (var edge in edges)
            {
                var provenance = edge.Provenance ?? ForgeGraphRelationshipProvenance.FromDeclaration(edge);
                dot.AppendLine($"  \"{Escape(edge.SourceId)}\" -> \"{Escape(edge.TargetId)}\" [label=\"{edge.Relationship}\", tooltip=\"{Escape(provenance.SourceKind)}: {Escape(provenance.SourceId)}\"];");
            }
            dot.AppendLine("}"); File.WriteAllText(dialog.FileName, dot.ToString(), Encoding.UTF8);
        }
    }

    private static string Escape(string? value) => (value ?? string.Empty).Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
}
