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

namespace RimForge.App.Features.ForgeView;

public sealed class ModNavigationRequestedEventArgs : EventArgs
{
    public ModNavigationRequestedEventArgs(string packageId) => PackageId = packageId;
    public string PackageId { get; }
}

public partial class ForgeViewView : UserControl
{
    private readonly List<string> _selectionHistory = new();
    private int _selectionHistoryIndex = -1;
    private bool _navigatingHistory;
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
        RequestLayoutSave();
    }

    private void GraphFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GraphCanvas is null || HealthFilterCombo is null || RelationshipFilterCombo is null) return;
        GraphCanvas.HealthFilter = SelectedTag(HealthFilterCombo, "All");
        GraphCanvas.RelationshipFilter = SelectedTag(RelationshipFilterCombo, "All");
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
        if (e.PropertyName == nameof(MainWindow.SelectedProfile)) LoadProfileLayout(force: false);
        if (e.PropertyName is nameof(MainWindow.SearchText) or nameof(MainWindow.SearchMatchedPackageIds)) RebuildOutline();
        if (e.PropertyName == nameof(MainWindow.SelectedMod) && DataContext is MainWindow window)
            PinNodeButton.Content = GraphCanvas.PinnedPackageIds.Contains(window.SelectedMod?.PackageId ?? string.Empty, StringComparer.OrdinalIgnoreCase) ? "Unpin Node" : "Pin Node";
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
                GraphCanvas.IsolateFocusedPath);
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
        bool IsolateFocusedPath = false);

    private void PreviousSelection_Click(object sender, RoutedEventArgs e) => NavigateSelectionHistory(-1);
    private void NextSelection_Click(object sender, RoutedEventArgs e) => NavigateSelectionHistory(1);

    private void RecordSelection(string packageId)
    {
        if (_navigatingHistory || string.IsNullOrWhiteSpace(packageId)) return;
        if (_selectionHistoryIndex >= 0 && string.Equals(_selectionHistory[_selectionHistoryIndex], packageId, StringComparison.OrdinalIgnoreCase)) return;
        if (_selectionHistoryIndex < _selectionHistory.Count - 1)
            _selectionHistory.RemoveRange(_selectionHistoryIndex + 1, _selectionHistory.Count - _selectionHistoryIndex - 1);
        _selectionHistory.Add(packageId);
        _selectionHistoryIndex = _selectionHistory.Count - 1;
        UpdateSelectionHistoryButtons();
    }

    private void NavigateSelectionHistory(int offset)
    {
        var next = _selectionHistoryIndex + offset;
        if (next < 0 || next >= _selectionHistory.Count) return;
        _selectionHistoryIndex = next;
        var packageId = _selectionHistory[next];
        _navigatingHistory = true;
        try
        {
            ModNavigationRequested?.Invoke(this, new ModNavigationRequestedEventArgs(packageId));
            GraphCanvas.CenterOnPackage(packageId);
        }
        finally
        {
            _navigatingHistory = false;
            UpdateSelectionHistoryButtons();
        }
    }

    private void UpdateSelectionHistoryButtons()
    {
        PreviousSelectionButton.IsEnabled = _selectionHistoryIndex > 0;
        NextSelectionButton.IsEnabled = _selectionHistoryIndex >= 0 && _selectionHistoryIndex < _selectionHistory.Count - 1;
    }

    private void RebuildOutline()
    {
        if (DataContext is not MainWindow window) return;
        var nodes = new Dictionary<string, DependencyGraphNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in window.DependencyNodes)
        {
            var key = !string.IsNullOrWhiteSpace(node.PackageId) ? node.PackageId.Trim() : node.Id?.Trim();
            if (string.IsNullOrWhiteSpace(key) || nodes.ContainsKey(key)) continue;
            nodes.Add(key, node);
        }

        var edges = window.DependencyEdges
            .Where(edge => !string.IsNullOrWhiteSpace(edge.SourceId) && !string.IsNullOrWhiteSpace(edge.TargetId))
            .Where(edge => nodes.ContainsKey(edge.SourceId) && nodes.ContainsKey(edge.TargetId))
            .ToList();
        var active = window.SelectedProfile?.ActiveMods.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!window.ShowFullLibrary && active is { Count: > 0 })
        {
            nodes = nodes.Where(pair => active.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            edges = edges.Where(edge => nodes.ContainsKey(edge.SourceId) && nodes.ContainsKey(edge.TargetId)).ToList();
        }

        if (window.IsSearchActive)
        {
            var matches = window.SearchMatchedPackageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            nodes = nodes.Where(pair => matches.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            edges = edges.Where(edge => nodes.ContainsKey(edge.SourceId) && nodes.ContainsKey(edge.TargetId)).ToList();
        }

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
        button.Click += (_, _) => ModNavigationRequested?.Invoke(this, new ModNavigationRequestedEventArgs(id));
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
            var csv = new StringBuilder("Source,Target,Relationship,Description\r\n");
            foreach (var edge in edges) csv.AppendLine($"\"{Escape(edge.SourceId)}\",\"{Escape(edge.TargetId)}\",\"{edge.Relationship}\",\"{Escape(edge.Description)}\"");
            File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
        }
        else
        {
            var dot = new StringBuilder("digraph RimForge {\r\n  rankdir=LR;\r\n  node [shape=box, style=rounded];\r\n");
            foreach (var node in window.DependencyNodes) dot.AppendLine($"  \"{Escape(node.PackageId ?? node.Id)}\" [label=\"{Escape(node.Name)}\"];");
            foreach (var edge in edges) dot.AppendLine($"  \"{Escape(edge.SourceId)}\" -> \"{Escape(edge.TargetId)}\" [label=\"{edge.Relationship}\"];");
            dot.AppendLine("}"); File.WriteAllText(dialog.FileName, dot.ToString(), Encoding.UTF8);
        }
    }

    private static string Escape(string? value) => (value ?? string.Empty).Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
}
