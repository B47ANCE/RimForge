using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RimForge.Core.Models;

namespace RimForge.App.Features.ForgeView;

public sealed class ForgeGraphNodeInvokedEventArgs : EventArgs
{
    public ForgeGraphNodeInvokedEventArgs(string packageId) => PackageId = packageId;
    public string PackageId { get; }
}

public sealed class ForgeGraphNodeHoverChangedEventArgs : EventArgs
{
    public ForgeGraphNodeHoverChangedEventArgs(string? packageId) => PackageId = packageId;
    public string? PackageId { get; }
}

public sealed class ForgeGraphLayoutChangedEventArgs : EventArgs
{
    public ForgeGraphLayoutChangedEventArgs(string packageId, Point position, bool isPinned)
    {
        PackageId = packageId;
        Position = position;
        IsPinned = isPinned;
    }

    public string PackageId { get; }
    public Point Position { get; }
    public bool IsPinned { get; }
}

public sealed class ForgeGraphRenderCompletedEventArgs : EventArgs
{
    public ForgeGraphRenderCompletedEventArgs(int totalNodes, int renderedNodes, int totalEdges, int renderedEdges, TimeSpan elapsed, bool viewportCullingEnabled)
    {
        TotalNodes = totalNodes;
        RenderedNodes = renderedNodes;
        TotalEdges = totalEdges;
        RenderedEdges = renderedEdges;
        Elapsed = elapsed;
        ViewportCullingEnabled = viewportCullingEnabled;
    }

    public int TotalNodes { get; }
    public int RenderedNodes { get; }
    public int CulledNodes => Math.Max(0, TotalNodes - RenderedNodes);
    public int TotalEdges { get; }
    public int RenderedEdges { get; }
    public int CulledEdges => Math.Max(0, TotalEdges - RenderedEdges);
    public TimeSpan Elapsed { get; }
    public bool ViewportCullingEnabled { get; }
}

public sealed class ForgeGraphCanvas : FrameworkElement
{
    private const double NodeWidth = 190;
    private const double NodeHeight = 58;
    private const double HorizontalGap = 72;
    private const double VerticalGap = 34;
    private const int ViewportCullingThreshold = 120;
    private const double ViewportCullMargin = 180;
    private readonly Dictionary<string, Rect> _nodeRects = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Point> _layoutCache = new(StringComparer.OrdinalIgnoreCase);
    private string _layoutSignature = string.Empty;
    private Rect _logicalBounds = Rect.Empty;
    private Point _pan;
    private Point _dragOrigin;
    private Point _panOrigin;
    private bool _isPanning;
    private bool _isMinimapNavigating;
    private Rect _minimapBounds = Rect.Empty;
    private double _minimapScale;
    private Point _minimapContentOrigin;
    private bool _hasInitializedView;
    private double _zoom = 1;
    private string? _hoveredPackageId;
    private readonly Dictionary<string, Point> _customPositions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pinnedPackageIds = new(StringComparer.OrdinalIgnoreCase);
    private string? _draggedPackageId;
    private Point _draggedNodeOrigin;
    private bool _nodeDragMoved;

    public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(
        nameof(Nodes), typeof(IEnumerable), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnCollectionChanged));
    public static readonly DependencyProperty EdgesProperty = DependencyProperty.Register(
        nameof(Edges), typeof(IEnumerable), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnCollectionChanged));
    public static readonly DependencyProperty SelectedPackageIdProperty = DependencyProperty.Register(
        nameof(SelectedPackageId), typeof(string), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedPackageChanged));
    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
        nameof(SearchText), typeof(string), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnTopologyModeChanged));
    public static readonly DependencyProperty SearchMatchPackageIdsProperty = DependencyProperty.Register(
        nameof(SearchMatchPackageIds), typeof(IEnumerable), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnCollectionChanged));
    public static readonly DependencyProperty ProfilePackageIdsProperty = DependencyProperty.Register(
        nameof(ProfilePackageIds), typeof(IEnumerable), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnCollectionChanged));
    public static readonly DependencyProperty ShowFullLibraryProperty = DependencyProperty.Register(
        nameof(ShowFullLibrary), typeof(bool), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, OnTopologyModeChanged));
    public static readonly DependencyProperty HighlightDependencyPathsProperty = DependencyProperty.Register(
        nameof(HighlightDependencyPaths), typeof(bool), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ShowMinimapProperty = DependencyProperty.Register(
        nameof(ShowMinimap), typeof(bool), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsLayoutEditModeProperty = DependencyProperty.Register(
        nameof(IsLayoutEditMode), typeof(bool), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HealthFilterProperty = DependencyProperty.Register(
        nameof(HealthFilter), typeof(string), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata("All", FrameworkPropertyMetadataOptions.AffectsRender, OnTopologyModeChanged));
    public static readonly DependencyProperty RelationshipFilterProperty = DependencyProperty.Register(
        nameof(RelationshipFilter), typeof(string), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata("All", FrameworkPropertyMetadataOptions.AffectsRender, OnTopologyModeChanged));
    public static readonly DependencyProperty IsolateFocusedPathProperty = DependencyProperty.Register(
        nameof(IsolateFocusedPath), typeof(bool), typeof(ForgeGraphCanvas), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnTopologyModeChanged));

    public IEnumerable? Nodes { get => (IEnumerable?)GetValue(NodesProperty); set => SetValue(NodesProperty, value); }
    public IEnumerable? Edges { get => (IEnumerable?)GetValue(EdgesProperty); set => SetValue(EdgesProperty, value); }
    public string? SelectedPackageId { get => (string?)GetValue(SelectedPackageIdProperty); set => SetValue(SelectedPackageIdProperty, value); }
    public string? SearchText { get => (string?)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public IEnumerable? SearchMatchPackageIds { get => (IEnumerable?)GetValue(SearchMatchPackageIdsProperty); set => SetValue(SearchMatchPackageIdsProperty, value); }
    public IEnumerable? ProfilePackageIds { get => (IEnumerable?)GetValue(ProfilePackageIdsProperty); set => SetValue(ProfilePackageIdsProperty, value); }
    public bool ShowFullLibrary { get => (bool)GetValue(ShowFullLibraryProperty); set => SetValue(ShowFullLibraryProperty, value); }
    public bool HighlightDependencyPaths { get => (bool)GetValue(HighlightDependencyPathsProperty); set => SetValue(HighlightDependencyPathsProperty, value); }
    public bool ShowMinimap { get => (bool)GetValue(ShowMinimapProperty); set => SetValue(ShowMinimapProperty, value); }
    public bool IsLayoutEditMode { get => (bool)GetValue(IsLayoutEditModeProperty); set => SetValue(IsLayoutEditModeProperty, value); }
    public string HealthFilter { get => (string)GetValue(HealthFilterProperty); set => SetValue(HealthFilterProperty, value); }
    public string RelationshipFilter { get => (string)GetValue(RelationshipFilterProperty); set => SetValue(RelationshipFilterProperty, value); }
    public bool IsolateFocusedPath { get => (bool)GetValue(IsolateFocusedPathProperty); set => SetValue(IsolateFocusedPathProperty, value); }
    public double Zoom => _zoom;

    public event EventHandler<ForgeGraphNodeInvokedEventArgs>? NodeInvoked;
    public event EventHandler<ForgeGraphNodeHoverChangedEventArgs>? NodeHoverChanged;
    public event EventHandler<ForgeGraphRenderCompletedEventArgs>? RenderCompleted;
    public event EventHandler<ForgeGraphLayoutChangedEventArgs>? LayoutChanged;
    public event EventHandler? ViewChanged;

    public ForgeGraphCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = Cursors.Hand;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseRightButtonDown += OnMouseRightButtonDown;
        MouseRightButtonUp += OnMouseRightButtonUp;
        KeyDown += OnKeyDown;
        SizeChanged += (_, _) => { if (!_hasInitializedView) FitToView(); };
    }

    private static void OnCollectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ForgeGraphCanvas canvas) return;
        if (e.OldValue is INotifyCollectionChanged oldCollection) oldCollection.CollectionChanged -= canvas.SourceCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newCollection) newCollection.CollectionChanged += canvas.SourceCollectionChanged;
        canvas.InvalidateLayoutCache();
    }

    private static void OnSelectedPackageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ForgeGraphCanvas canvas) return;

        canvas.InvalidateVisual();
        var packageId = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(packageId) || !canvas.IsLoaded) return;

        // Initial bindings often select a mod before graph topology has arrived. Preserve the
        // first full-graph presentation; only later selections center the existing fitted view.
        canvas.Dispatcher.BeginInvoke(() =>
        {
            if (canvas._hasInitializedView) canvas.CenterOnPackage(packageId);
            else canvas.FitToView();
        });
    }

    private static void OnTopologyModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ForgeGraphCanvas canvas) canvas.InvalidateLayoutCache();
    }

    private void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateLayoutCache();

    private void InvalidateLayoutCache()
    {
        _layoutSignature = string.Empty;
        _layoutCache.Clear();
        _logicalBounds = Rect.Empty;
        _hasInitializedView = false;
        InvalidateVisual();

        // Nodes commonly arrive after Loaded. Queue the fit so the first real topology is
        // presented as a complete graph rather than at the fallback 100% origin.
        if (IsLoaded) Dispatcher.BeginInvoke(FitToView);
    }

    public void ResetView() => FitToView();

    public IReadOnlyDictionary<string, Point> CustomPositions => _customPositions;
    public IReadOnlyCollection<string> PinnedPackageIds => _pinnedPackageIds;
    public Point Pan => _pan;

    public void ApplyLayoutState(IReadOnlyDictionary<string, Point>? positions, IEnumerable<string>? pinnedPackageIds, double zoom, Point pan)
    {
        _customPositions.Clear();
        if (positions is not null)
            foreach (var pair in positions)
                _customPositions[pair.Key] = pair.Value;
        _pinnedPackageIds.Clear();
        if (pinnedPackageIds is not null)
            foreach (var packageId in pinnedPackageIds.Where(value => !string.IsNullOrWhiteSpace(value)))
                _pinnedPackageIds.Add(packageId);
        _zoom = Math.Clamp(zoom <= 0 ? 1 : zoom, .35, 2.5);
        _pan = pan;
        _layoutSignature = string.Empty;
        _hasInitializedView = true;
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetCustomLayout()
    {
        _customPositions.Clear();
        _pinnedPackageIds.Clear();
        _layoutSignature = string.Empty;
        FitToView();
    }

    public bool TogglePin(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return false;
        EnsureLayout();
        if (!_layoutCache.TryGetValue(packageId, out var position)) return false;
        var pinned = !_pinnedPackageIds.Remove(packageId);
        if (pinned) _pinnedPackageIds.Add(packageId);
        _customPositions[packageId] = position;
        LayoutChanged?.Invoke(this, new ForgeGraphLayoutChangedEventArgs(packageId, position, pinned));
        InvalidateVisual();
        return pinned;
    }

    public void FitToView()
    {
        EnsureLayout();
        if (_logicalBounds.IsEmpty || ActualWidth <= 0 || ActualHeight <= 0)
        {
            _zoom = 1;
            _pan = new Point(24, 24);
            _hasInitializedView = false;
            InvalidateVisual();
            ViewChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        else
        {
            const double padding = 42;
            var availableWidth = Math.Max(1, ActualWidth - padding * 2);
            var availableHeight = Math.Max(1, ActualHeight - padding * 2);
            _zoom = Math.Clamp(Math.Min(availableWidth / _logicalBounds.Width, availableHeight / _logicalBounds.Height), .35, 1.65);
            _pan = new Point(
                (ActualWidth - _logicalBounds.Width * _zoom) / 2 - _logicalBounds.X * _zoom,
                (ActualHeight - _logicalBounds.Height * _zoom) / 2 - _logicalBounds.Y * _zoom);
        }
        _hasInitializedView = true;
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CenterOnPackage(string? packageId, bool preserveZoom = true)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return false;
        EnsureLayout();
        if (!_layoutCache.TryGetValue(packageId, out var logical)) return false;
        if (!preserveZoom) _zoom = Math.Clamp(_zoom, .65, 1.35);
        _pan = new Point(
            ActualWidth / 2 - (logical.X + NodeWidth / 2) * _zoom,
            ActualHeight / 2 - (logical.Y + NodeHeight / 2) * _zoom);
        _hasInitializedView = true;
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void ZoomIn() => SetZoom(_zoom * 1.15, new Point(ActualWidth / 2, ActualHeight / 2));
    public void ZoomOut() => SetZoom(_zoom / 1.15, new Point(ActualWidth / 2, ActualHeight / 2));

    protected override void OnRender(DrawingContext dc)
    {
        var stopwatch = Stopwatch.StartNew();
        base.OnRender(dc);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(25, 26, 29)), null, new Rect(RenderSize));
        DrawGrid(dc);

        var (nodes, edges) = GetVisibleTopology();
        if (nodes.Count == 0)
        {
            DrawCenteredText(dc, "No dependency graph data is available. Run or refresh the Forge scan.");
            PublishRenderMetrics(0, 0, 0, 0, stopwatch.Elapsed, false);
            return;
        }

        EnsureLayout(nodes, edges);
        var cullingEnabled = nodes.Count >= ViewportCullingThreshold;
        var logicalViewport = GetLogicalViewport(ViewportCullMargin);
        var renderedNodeIds = cullingEnabled
            ? nodes.Select(node => node.PackageId ?? node.Id)
                .Where(id => _layoutCache.TryGetValue(id, out var point) && logicalViewport.IntersectsWith(new Rect(point.X, point.Y, NodeWidth, NodeHeight)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : nodes.Select(node => node.PackageId ?? node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _nodeRects.Clear();
        foreach (var id in renderedNodeIds)
            if (_layoutCache.TryGetValue(id, out var point))
                _nodeRects[id] = Transform(new Rect(point.X, point.Y, NodeWidth, NodeHeight));

        var pathNodes = HighlightDependencyPaths ? BuildFocusedNodeSet(edges) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasPathFocus = pathNodes.Count > 1;
        var renderedEdges = 0;

        foreach (var edge in edges)
        {
            if (!_layoutCache.TryGetValue(edge.SourceId, out var source) || !_layoutCache.TryGetValue(edge.TargetId, out var target)) continue;
            if (cullingEnabled && !EdgeIntersectsViewport(source, target, logicalViewport)) continue;
            var onPath = pathNodes.Contains(edge.SourceId) && pathNodes.Contains(edge.TargetId);
            DrawEdge(dc, edge, source, target, onPath, hasPathFocus);
            renderedEdges++;
        }
        foreach (var node in nodes)
        {
            var id = node.PackageId ?? node.Id;
            if (!renderedNodeIds.Contains(id)) continue;
            if (_layoutCache.TryGetValue(id, out var position)) DrawNode(dc, node, position, pathNodes.Contains(id), hasPathFocus);
        }

        if (ShowMinimap) DrawMinimap(dc, nodes, edges);
        PublishRenderMetrics(nodes.Count, renderedNodeIds.Count, edges.Count, renderedEdges, stopwatch.Elapsed, cullingEnabled);
    }

    private Rect GetLogicalViewport(double margin)
    {
        if (_zoom <= 0) return Rect.Empty;
        return new Rect(
            (-_pan.X) / _zoom - margin,
            (-_pan.Y) / _zoom - margin,
            ActualWidth / _zoom + margin * 2,
            ActualHeight / _zoom + margin * 2);
    }

    private static bool EdgeIntersectsViewport(Point source, Point target, Rect viewport)
    {
        var left = Math.Min(source.X, target.X);
        var top = Math.Min(source.Y, target.Y);
        var right = Math.Max(source.X + NodeWidth, target.X + NodeWidth);
        var bottom = Math.Max(source.Y + NodeHeight, target.Y + NodeHeight);
        return viewport.IntersectsWith(new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top)));
    }

    private void PublishRenderMetrics(int totalNodes, int renderedNodes, int totalEdges, int renderedEdges, TimeSpan elapsed, bool cullingEnabled) =>
        RenderCompleted?.Invoke(this, new ForgeGraphRenderCompletedEventArgs(totalNodes, renderedNodes, totalEdges, renderedEdges, elapsed, cullingEnabled));

    private (List<DependencyGraphNode> Nodes, List<DependencyGraphEdge> Edges) GetVisibleTopology()
    {
        var nodes = Enumerate<DependencyGraphNode>(Nodes).ToList();
        var edges = Enumerate<DependencyGraphEdge>(Edges).ToList();

        if (!ShowFullLibrary)
        {
            var active = Enumerate<object>(ProfilePackageIds).Select(value => value?.ToString()).Where(value => !string.IsNullOrWhiteSpace(value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // An unavailable profile must not make a successfully discovered graph appear empty.
            if (active.Count > 0)
                nodes = nodes.Where(node => active.Contains(node.PackageId ?? node.Id)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchMatches = Enumerate<object>(SearchMatchPackageIds)
                .Select(value => value?.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            nodes = nodes.Where(node => searchMatches.Contains(node.PackageId ?? node.Id)).ToList();
        }

        nodes = ApplyHealthFilter(nodes);
        var ids = nodes.Select(node => node.PackageId ?? node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        edges = edges
            .Where(edge => ids.Contains(edge.SourceId) && ids.Contains(edge.TargetId))
            .Where(ForgeGraphPresentationPolicy.ShouldDisplayEdge)
            .Where(MatchesRelationshipFilter)
            .ToList();

        if (IsolateFocusedPath && !string.IsNullOrWhiteSpace(SelectedPackageId) && ids.Contains(SelectedPackageId))
        {
            var pathIds = BuildFocusedNodeSet(edges);
            nodes = nodes.Where(node => pathIds.Contains(node.PackageId ?? node.Id)).ToList();
            ids = nodes.Select(node => node.PackageId ?? node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            edges = edges.Where(edge => ids.Contains(edge.SourceId) && ids.Contains(edge.TargetId)).ToList();
        }

        return (nodes, edges);
    }

    private List<DependencyGraphNode> ApplyHealthFilter(List<DependencyGraphNode> nodes)
    {
        if (string.IsNullOrWhiteSpace(HealthFilter) || HealthFilter.Equals("All", StringComparison.OrdinalIgnoreCase)) return nodes;
        return nodes.Where(node => node.Status.ToString().Equals(HealthFilter, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private bool MatchesRelationshipFilter(DependencyGraphEdge edge)
    {
        if (string.IsNullOrWhiteSpace(RelationshipFilter) || RelationshipFilter.Equals("All", StringComparison.OrdinalIgnoreCase)) return true;
        return RelationshipFilter switch
        {
            "Required" => edge.Relationship is DependencyRelationshipType.Required or DependencyRelationshipType.PatchTarget,
            "Optional" => edge.Relationship == DependencyRelationshipType.Optional,
            "Ordering" => edge.Relationship is DependencyRelationshipType.LoadBefore or DependencyRelationshipType.LoadAfter,
            "Conflicts" => edge.Relationship == DependencyRelationshipType.Incompatible,
            _ => true
        };
    }

    private void EnsureLayout()
    {
        var (nodes, edges) = GetVisibleTopology();
        EnsureLayout(nodes, edges);
    }

    private void EnsureLayout(IReadOnlyList<DependencyGraphNode> nodes, IReadOnlyList<DependencyGraphEdge> edges)
    {
        var signature = string.Join("|", nodes.Select(n => n.PackageId ?? n.Id).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) + "::" +
                        string.Join("|", edges.Select(e => $"{e.SourceId}>{e.TargetId}:{e.Relationship}").OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        if (string.Equals(signature, _layoutSignature, StringComparison.Ordinal)) return;
        _layoutSignature = signature;
        _layoutCache = Layout(nodes, edges);
        foreach (var pair in _customPositions)
            if (_layoutCache.ContainsKey(pair.Key))
                _layoutCache[pair.Key] = pair.Value;
        _logicalBounds = CalculateLogicalBounds(_layoutCache.Values);
    }

    private static Rect CalculateLogicalBounds(IEnumerable<Point> positions)
    {
        var points = positions.ToList();
        if (points.Count == 0) return Rect.Empty;
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X) + NodeWidth;
        var maxY = points.Max(p => p.Y) + NodeHeight;
        return new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
    }

    private static Dictionary<string, Point> Layout(IReadOnlyList<DependencyGraphNode> nodes, IReadOnlyList<DependencyGraphEdge> edges)
    {
        var nodeById = nodes
            .GroupBy(node => node.PackageId ?? node.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var ids = nodeById.Keys.OrderBy(NodeOrderKey, StringComparer.OrdinalIgnoreCase).ToArray();
        var validEdges = edges
            .Where(edge => nodeById.ContainsKey(edge.SourceId) && nodeById.ContainsKey(edge.TargetId))
            .Where(edge => !string.Equals(edge.SourceId, edge.TargetId, StringComparison.OrdinalIgnoreCase))
            .GroupBy(edge => $"{edge.SourceId}>{edge.TargetId}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        var undirected = ids.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        foreach (var edge in validEdges)
        {
            undirected[edge.SourceId].Add(edge.TargetId);
            undirected[edge.TargetId].Add(edge.SourceId);
        }

        var components = BuildConnectedComponents(ids, undirected)
            .OrderByDescending(component => component.Count)
            .ThenBy(component => MinimumNodeOrderKey(component), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var componentLayouts = components
            .Select(component => LayoutConnectedComponent(component, validEdges, nodeById))
            .ToArray();

        const double componentGap = 150;
        var totalArea = componentLayouts.Sum(layout => Math.Max(NodeWidth, layout.Bounds.Width) * Math.Max(NodeHeight, layout.Bounds.Height));
        var targetRowWidth = Math.Max(1300, Math.Sqrt(Math.Max(1, totalArea)) * 1.65);
        var result = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
        var cursorX = 0d;
        var cursorY = 0d;
        var rowHeight = 0d;

        foreach (var layout in componentLayouts)
        {
            var width = Math.Max(NodeWidth, layout.Bounds.Width);
            var height = Math.Max(NodeHeight, layout.Bounds.Height);
            if (cursorX > 0 && cursorX + width > targetRowWidth)
            {
                cursorX = 0;
                cursorY += rowHeight + componentGap;
                rowHeight = 0;
            }

            foreach (var pair in layout.Positions)
                result[pair.Key] = new Point(pair.Value.X - layout.Bounds.X + cursorX, pair.Value.Y - layout.Bounds.Y + cursorY);

            cursorX += width + componentGap;
            rowHeight = Math.Max(rowHeight, height);
        }

        return result;
    }

    private static IReadOnlyList<HashSet<string>> BuildConnectedComponents(
        IReadOnlyList<string> ids,
        IReadOnlyDictionary<string, HashSet<string>> undirected)
    {
        var remaining = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<HashSet<string>>();
        foreach (var seed in ids)
        {
            if (!remaining.Remove(seed)) continue;
            var component = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { seed };
            var queue = new Queue<string>();
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in undirected[current].OrderBy(NodeOrderKey, StringComparer.OrdinalIgnoreCase))
                {
                    if (!remaining.Remove(neighbor)) continue;
                    component.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
            result.Add(component);
        }
        return result;
    }

    private static ComponentLayout LayoutConnectedComponent(
        HashSet<string> component,
        IReadOnlyList<DependencyGraphEdge> allEdges,
        IReadOnlyDictionary<string, DependencyGraphNode> nodeById)
    {
        var edges = allEdges.Where(edge => component.Contains(edge.SourceId) && component.Contains(edge.TargetId)).ToArray();
        var dependencyToDependents = component.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges)
            dependencyToDependents[edge.TargetId].Add(edge.SourceId);

        var stronglyConnected = FindStronglyConnectedComponents(component, dependencyToDependents);
        var componentIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < stronglyConnected.Count; index++)
            foreach (var id in stronglyConnected[index])
                componentIndex[id] = index;

        var dag = Enumerable.Range(0, stronglyConnected.Count).ToDictionary(index => index, _ => new HashSet<int>());
        var indegree = Enumerable.Range(0, stronglyConnected.Count).ToDictionary(index => index, _ => 0);
        foreach (var edge in edges)
        {
            var from = componentIndex[edge.TargetId];
            var to = componentIndex[edge.SourceId];
            if (from == to || !dag[from].Add(to)) continue;
            indegree[to]++;
        }

        var levelByComponent = Enumerable.Range(0, stronglyConnected.Count).ToDictionary(index => index, _ => 0);
        var ready = new SortedSet<int>(Comparer<int>.Create((left, right) =>
        {
            if (left == right) return 0;
            var compare = StringComparer.OrdinalIgnoreCase.Compare(
                MinimumNodeOrderKey(stronglyConnected[left]),
                MinimumNodeOrderKey(stronglyConnected[right]));
            return compare != 0 ? compare : left.CompareTo(right);
        }));
        foreach (var pair in indegree.Where(pair => pair.Value == 0)) ready.Add(pair.Key);

        while (ready.Count > 0)
        {
            var current = ready.Min;
            ready.Remove(current);
            foreach (var next in dag[current])
            {
                levelByComponent[next] = Math.Max(levelByComponent[next], levelByComponent[current] + 1);
                if (--indegree[next] == 0) ready.Add(next);
            }
        }

        var levelByNode = component.ToDictionary(id => id, id => levelByComponent[componentIndex[id]], StringComparer.OrdinalIgnoreCase);
        var layers = levelByNode
            .GroupBy(pair => pair.Value)
            .OrderBy(group => group.Key)
            .ToDictionary(
                group => group.Key,
                group => group.Select(pair => pair.Key).OrderBy(id => NodeSortKey(nodeById[id]), StringComparer.OrdinalIgnoreCase).ToList());

        OptimizeLayerOrdering(layers, edges);

        var maxLayerHeight = layers.Values.Max(layer => Math.Max(1, layer.Count) * NodeHeight + Math.Max(0, layer.Count - 1) * VerticalGap);
        var dynamicHorizontalGap = Math.Clamp(HorizontalGap + Math.Sqrt(Math.Max(1, edges.Length)) * 5, 88, 156);
        var positions = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in layers)
        {
            var layerHeight = Math.Max(1, pair.Value.Count) * NodeHeight + Math.Max(0, pair.Value.Count - 1) * VerticalGap;
            var y = (maxLayerHeight - layerHeight) / 2;
            foreach (var id in pair.Value)
            {
                positions[id] = new Point(pair.Key * (NodeWidth + dynamicHorizontalGap), y);
                y += NodeHeight + VerticalGap;
            }
        }

        return new ComponentLayout(positions, CalculateLogicalBounds(positions.Values));
    }

    private static void OptimizeLayerOrdering(
        Dictionary<int, List<string>> layers,
        IReadOnlyList<DependencyGraphEdge> edges)
    {
        if (layers.Count <= 1) return;
        var neighbors = layers.Values.SelectMany(layer => layer)
            .ToDictionary(id => id, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges)
        {
            neighbors[edge.SourceId].Add(edge.TargetId);
            neighbors[edge.TargetId].Add(edge.SourceId);
        }

        var orderedLevels = layers.Keys.OrderBy(level => level).ToArray();
        for (var pass = 0; pass < 6; pass++)
        {
            var sweep = pass % 2 == 0 ? orderedLevels : orderedLevels.Reverse();
            foreach (var level in sweep)
            {
                var current = layers[level];
                var relevantLevel = pass % 2 == 0 ? level - 1 : level + 1;
                if (!layers.TryGetValue(relevantLevel, out var reference)) continue;
                var referenceIndex = reference.Select((id, index) => (id, index))
                    .ToDictionary(pair => pair.id, pair => pair.index, StringComparer.OrdinalIgnoreCase);
                current.Sort((left, right) =>
                {
                    var leftBarycenter = Barycenter(neighbors[left], referenceIndex);
                    var rightBarycenter = Barycenter(neighbors[right], referenceIndex);
                    var compare = leftBarycenter.CompareTo(rightBarycenter);
                    return compare != 0 ? compare : StringComparer.OrdinalIgnoreCase.Compare(NodeOrderKey(left), NodeOrderKey(right));
                });
            }
        }
    }

    private static double Barycenter(IEnumerable<string> neighbors, IReadOnlyDictionary<string, int> referenceIndex)
    {
        var indexes = neighbors.Where(referenceIndex.ContainsKey).Select(id => referenceIndex[id]).ToArray();
        return indexes.Length == 0 ? double.MaxValue : indexes.Average();
    }

    private static IReadOnlyList<List<string>> FindStronglyConnectedComponents(
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        var nextIndex = 0;
        var indexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lowLinkById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<List<string>>();

        void Visit(string id)
        {
            indexById[id] = nextIndex;
            lowLinkById[id] = nextIndex;
            nextIndex++;
            stack.Push(id);
            onStack.Add(id);

            foreach (var next in adjacency[id].OrderBy(NodeOrderKey, StringComparer.OrdinalIgnoreCase))
            {
                if (!indexById.ContainsKey(next))
                {
                    Visit(next);
                    lowLinkById[id] = Math.Min(lowLinkById[id], lowLinkById[next]);
                }
                else if (onStack.Contains(next))
                {
                    lowLinkById[id] = Math.Min(lowLinkById[id], indexById[next]);
                }
            }

            if (lowLinkById[id] != indexById[id]) return;
            var stronglyConnected = new List<string>();
            string current;
            do
            {
                current = stack.Pop();
                onStack.Remove(current);
                stronglyConnected.Add(current);
            } while (!string.Equals(current, id, StringComparison.OrdinalIgnoreCase));
            stronglyConnected.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(NodeOrderKey(left), NodeOrderKey(right)));
            result.Add(stronglyConnected);
        }

        foreach (var id in ids.OrderBy(NodeOrderKey, StringComparer.OrdinalIgnoreCase))
            if (!indexById.ContainsKey(id)) Visit(id);
        return result;
    }

    private static string NodeSortKey(DependencyGraphNode node) => $"{NodeOrderKey(node.PackageId ?? node.Id)}|{node.Name}";

    private static string MinimumNodeOrderKey(IEnumerable<string> packageIds) =>
        packageIds.Select(NodeOrderKey).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).First();

    private static string NodeOrderKey(string packageId)
    {
        if (LoadOrderRules.IsCore(packageId)) return $"0|{packageId}";
        if (LoadOrderRules.IsOfficialDlc(packageId)) return $"1|{packageId}";
        return $"2|{packageId}";
    }

    private sealed record ComponentLayout(Dictionary<string, Point> Positions, Rect Bounds);

    private HashSet<string> BuildFocusedNodeSet(IReadOnlyList<DependencyGraphEdge> edges)
    {
        var selected = SelectedPackageId;
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(selected)) return result;
        result.Add(selected);
        Traverse(selected, edges, result, upstream: true);
        Traverse(selected, edges, result, upstream: false);
        return result;
    }

    private static void Traverse(string start, IReadOnlyList<DependencyGraphEdge> edges, HashSet<string> visited, bool upstream)
    {
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var nextIds = upstream
                ? edges.Where(e => string.Equals(e.SourceId, current, StringComparison.OrdinalIgnoreCase)).Select(e => e.TargetId)
                : edges.Where(e => string.Equals(e.TargetId, current, StringComparison.OrdinalIgnoreCase)).Select(e => e.SourceId);
            foreach (var next in nextIds)
                if (visited.Add(next)) queue.Enqueue(next);
        }
    }

    private void DrawNode(DrawingContext dc, DependencyGraphNode node, Point logical, bool onPath, bool hasPathFocus)
    {
        var id = node.PackageId ?? node.Id;
        var rect = Transform(new Rect(logical.X, logical.Y, NodeWidth, NodeHeight));
        var selected = string.Equals(id, SelectedPackageId, StringComparison.OrdinalIgnoreCase);
        var match = !string.IsNullOrWhiteSpace(SearchText) && (id.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || node.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        var muted = hasPathFocus && !onPath;
        var fill = selected ? Color.FromRgb(84, 54, 32) : match ? Color.FromRgb(62, 49, 34) : Color.FromRgb(47, 49, 54);
        var border = selected || match || onPath ? Color.FromRgb(242, 140, 40) : StatusColor(node.Status);
        var fillBrush = new SolidColorBrush(fill) { Opacity = muted ? .34 : 1 };
        var borderBrush = new SolidColorBrush(border) { Opacity = muted ? .28 : 1 };
        dc.DrawRoundedRectangle(fillBrush, new Pen(borderBrush, selected ? 2.8 : onPath ? 1.8 : 1.2), rect, 8 * _zoom, 8 * _zoom);
        if (_pinnedPackageIds.Contains(id))
        {
            var rivetCenter = new Point(rect.Right - 11 * _zoom, rect.Top + 11 * _zoom);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(184, 134, 62)), new Pen(new SolidColorBrush(Color.FromRgb(235, 190, 95)), Math.Max(1, _zoom)), rivetCenter, 4.2 * _zoom, 4.2 * _zoom);
        }
        var primary = new SolidColorBrush(Colors.White) { Opacity = muted ? .32 : 1 };
        var secondary = new SolidColorBrush(Color.FromRgb(180, 183, 190)) { Opacity = muted ? .28 : 1 };
        DrawText(dc, node.Name, rect.X + 12 * _zoom, rect.Y + 9 * _zoom, 13 * _zoom, primary, FontWeights.SemiBold, rect.Width - 24 * _zoom);
        DrawText(dc, id, rect.X + 12 * _zoom, rect.Y + 32 * _zoom, 10 * _zoom, secondary, FontWeights.Normal, rect.Width - 24 * _zoom);
    }

    private void DrawEdge(DrawingContext dc, DependencyGraphEdge edge, Point source, Point target, bool onPath, bool hasPathFocus)
    {
        var sourceRect = Transform(new Rect(source.X, source.Y, NodeWidth, NodeHeight));
        var targetRect = Transform(new Rect(target.X, target.Y, NodeWidth, NodeHeight));
        var sourcePort = GetEdgePort(sourceRect, GetCenter(targetRect));
        var targetPort = GetEdgePort(targetRect, GetCenter(sourceRect));
        var selectedIsSource = string.Equals(edge.SourceId, SelectedPackageId, StringComparison.OrdinalIgnoreCase);
        var selectedIsTarget = string.Equals(edge.TargetId, SelectedPackageId, StringComparison.OrdinalIgnoreCase);
        var incidentToSelection = selectedIsSource || selectedIsTarget;
        var color = GetEdgeColor(edge.Relationship, selectedIsSource, selectedIsTarget);
        var opacity = hasPathFocus
            ? onPath ? incidentToSelection ? 1 : .72 : .1
            : incidentToSelection ? 1 : .62;
        var thickness = incidentToSelection ? 3.2 : onPath ? 2.35 : 1.25;
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        var pen = new Pen(brush, thickness * _zoom)
        {
            DashCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.DashStyle = GetEdgeDashStyle(edge.Relationship);

        var directDistance = (targetPort.Point - sourcePort.Point).Length;
        var controlDistance = Math.Clamp(directDistance * .32, 24 * _zoom, 118 * _zoom);
        var sourceControl = sourcePort.Point + sourcePort.Normal * controlDistance;
        var targetControl = targetPort.Point + targetPort.Normal * controlDistance;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(sourcePort.Point, false, false);
            ctx.BezierTo(sourceControl, targetControl, targetPort.Point, true, false);
        }
        dc.DrawGeometry(null, pen, geometry);

        if (opacity < .3) return;
        var drawStartArrow = edge.Relationship is DependencyRelationshipType.Incompatible or DependencyRelationshipType.LoadAfter;
        var drawEndArrow = edge.Relationship != DependencyRelationshipType.LoadAfter;
        if (drawStartArrow) DrawArrowHead(dc, sourcePort.Point, sourceControl, color, opacity);
        if (drawEndArrow) DrawArrowHead(dc, targetPort.Point, targetControl, color, opacity);
    }

    private static Point GetCenter(Rect rect) => new(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

    private static EdgePort GetEdgePort(Rect rect, Point toward)
    {
        var center = GetCenter(rect);
        var delta = toward - center;
        if (delta.Length < .01)
            return new EdgePort(new Point(rect.Right, center.Y), new Vector(1, 0));

        var horizontalScale = Math.Abs(delta.X) < .001 ? double.PositiveInfinity : rect.Width / 2 / Math.Abs(delta.X);
        var verticalScale = Math.Abs(delta.Y) < .001 ? double.PositiveInfinity : rect.Height / 2 / Math.Abs(delta.Y);
        var scale = Math.Min(horizontalScale, verticalScale);
        var point = center + delta * scale;
        var normal = horizontalScale <= verticalScale
            ? new Vector(delta.X >= 0 ? 1 : -1, 0)
            : new Vector(0, delta.Y >= 0 ? 1 : -1);
        return new EdgePort(point, normal);
    }

    private static Color GetEdgeColor(DependencyRelationshipType relationship, bool selectedIsSource, bool selectedIsTarget)
    {
        if (relationship == DependencyRelationshipType.Incompatible) return Color.FromRgb(230, 76, 76);
        if (selectedIsSource) return Color.FromRgb(242, 140, 40);
        if (selectedIsTarget) return Color.FromRgb(62, 198, 214);
        return relationship switch
        {
            DependencyRelationshipType.Optional => Color.FromRgb(77, 163, 255),
            DependencyRelationshipType.LoadBefore or DependencyRelationshipType.LoadAfter => Color.FromRgb(202, 151, 255),
            DependencyRelationshipType.PatchTarget => Color.FromRgb(110, 205, 146),
            _ => Color.FromRgb(166, 171, 181)
        };
    }

    private static DashStyle GetEdgeDashStyle(DependencyRelationshipType relationship) => relationship switch
    {
        DependencyRelationshipType.Optional => new DashStyle(new[] { 8d, 4d }, 0),
        DependencyRelationshipType.LoadBefore or DependencyRelationshipType.LoadAfter => new DashStyle(new[] { 8d, 3d, 1d, 3d }, 0),
        DependencyRelationshipType.Incompatible => new DashStyle(new[] { 2d, 2.4d }, 0),
        DependencyRelationshipType.PatchTarget => DashStyles.Dot,
        _ => DashStyles.Solid
    };

    private void DrawArrowHead(DrawingContext dc, Point tip, Point interiorControl, Color color, double opacity)
    {
        var direction = interiorControl - tip;
        if (direction.Length <= .01) return;
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var length = 10 * _zoom;
        var halfWidth = 4 * _zoom;
        var a = tip + direction * length + normal * halfWidth;
        var b = tip + direction * length - normal * halfWidth;
        var arrow = new StreamGeometry();
        using var arrowContext = arrow.Open();
        arrowContext.BeginFigure(tip, true, true);
        arrowContext.LineTo(a, true, false);
        arrowContext.LineTo(b, true, false);
        dc.DrawGeometry(new SolidColorBrush(color) { Opacity = opacity }, null, arrow);
    }

    private readonly record struct EdgePort(Point Point, Vector Normal);

    private void DrawMinimap(DrawingContext dc, IReadOnlyList<DependencyGraphNode> nodes, IReadOnlyList<DependencyGraphEdge> edges)
    {
        if (_logicalBounds.IsEmpty || ActualWidth < 420 || ActualHeight < 260) { _minimapBounds = Rect.Empty; return; }
        const double width = 180;
        const double height = 112;
        const double margin = 14;
        var box = new Rect(ActualWidth - width - margin, ActualHeight - height - margin, width, height);
        _minimapBounds = box;
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(224, 37, 37, 40)), new Pen(new SolidColorBrush(Color.FromRgb(74, 77, 84)), 1), box, 7, 7);
        var scale = Math.Min((width - 18) / _logicalBounds.Width, (height - 18) / _logicalBounds.Height);
        _minimapScale = scale;
        _minimapContentOrigin = new Point(box.X + 9, box.Y + 9);
        Point Mini(Point p) => new(_minimapContentOrigin.X + (p.X - _logicalBounds.X) * scale, _minimapContentOrigin.Y + (p.Y - _logicalBounds.Y) * scale);
        foreach (var edge in edges)
        {
            if (!_layoutCache.TryGetValue(edge.SourceId, out var source) || !_layoutCache.TryGetValue(edge.TargetId, out var target)) continue;
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(100, 160, 164, 173)), 1), Mini(new Point(source.X + NodeWidth / 2, source.Y + NodeHeight / 2)), Mini(new Point(target.X + NodeWidth / 2, target.Y + NodeHeight / 2)));
        }
        foreach (var node in nodes)
        {
            var id = node.PackageId ?? node.Id;
            if (!_layoutCache.TryGetValue(id, out var point)) continue;
            var selected = string.Equals(id, SelectedPackageId, StringComparison.OrdinalIgnoreCase);
            dc.DrawEllipse(new SolidColorBrush(selected ? Color.FromRgb(242, 140, 40) : StatusColor(node.Status)), null, Mini(new Point(point.X + NodeWidth / 2, point.Y + NodeHeight / 2)), selected ? 3.5 : 2, selected ? 3.5 : 2);
        }
        var viewportLogical = new Rect((-_pan.X) / _zoom, (-_pan.Y) / _zoom, ActualWidth / _zoom, ActualHeight / _zoom);
        var topLeft = Mini(viewportLogical.TopLeft);
        var viewport = new Rect(topLeft.X, topLeft.Y, viewportLogical.Width * scale, viewportLogical.Height * scale);
        viewport.Intersect(box);
        if (!viewport.IsEmpty) dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(242, 140, 40)), 1.2), viewport);
    }

    private void DrawGrid(DrawingContext dc)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), 1);
        var spacing = 32 * _zoom;
        if (spacing < 10) return;
        for (var x = _pan.X % spacing; x < ActualWidth; x += spacing) dc.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
        for (var y = _pan.Y % spacing; y < ActualHeight; y += spacing) dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
    }

    private Rect Transform(Rect rect) => new(rect.X * _zoom + _pan.X, rect.Y * _zoom + _pan.Y, rect.Width * _zoom, rect.Height * _zoom);
    private Point Transform(Point point) => new(point.X * _zoom + _pan.X, point.Y * _zoom + _pan.Y);

    public void ZoomAt(int wheelDelta, Point anchor) => SetZoom(_zoom * (wheelDelta > 0 ? 1.12 : .89), anchor);
    private void SetZoom(double value, Point anchor)
    {
        var next = Math.Clamp(value, .35, 2.5);
        var logical = new Point((anchor.X - _pan.X) / _zoom, (anchor.Y - _pan.Y) / _zoom);
        _zoom = next;
        _pan = new Point(anchor.X - logical.X * _zoom, anchor.Y - logical.Y * _zoom);
        _hasInitializedView = true;
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private string? HitTestNode(Point point)
    {
        var hit = _nodeRects.FirstOrDefault(pair => pair.Value.Contains(point));
        return string.IsNullOrWhiteSpace(hit.Key) ? null : hit.Key;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        var point = e.GetPosition(this);
        if (TryBeginMinimapNavigation(point))
        {
            CaptureMouse();
            e.Handled = true;
            return;
        }
        var hit = HitTestNode(point);
        if (!string.IsNullOrWhiteSpace(hit))
        {
            if (IsLayoutEditMode && _layoutCache.TryGetValue(hit, out var logical))
            {
                _draggedPackageId = hit;
                _dragOrigin = point;
                _draggedNodeOrigin = logical;
                _nodeDragMoved = false;
                CaptureMouse();
            }
            else
            {
                NodeInvoked?.Invoke(this, new ForgeGraphNodeInvokedEventArgs(hit));
                if (e.ClickCount > 1) CenterOnPackage(hit);
            }
            e.Handled = true;
            return;
        }
        BeginPan(point);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_draggedPackageId))
        {
            var packageId = _draggedPackageId;
            _draggedPackageId = null;
            if (!_nodeDragMoved) NodeInvoked?.Invoke(this, new ForgeGraphNodeInvokedEventArgs(packageId));
            else if (_layoutCache.TryGetValue(packageId, out var position))
                LayoutChanged?.Invoke(this, new ForgeGraphLayoutChangedEventArgs(packageId, position, _pinnedPackageIds.Contains(packageId)));
        }
        _isMinimapNavigating = false;
        EndPan();
        ReleaseMouseCapture();
    }
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var current = e.GetPosition(this);
        if (_isMinimapNavigating && e.LeftButton == MouseButtonState.Pressed)
        {
            NavigateFromMinimap(current);
            e.Handled = true;
            return;
        }
        if (!string.IsNullOrWhiteSpace(_draggedPackageId) && e.LeftButton == MouseButtonState.Pressed)
        {
            var delta = current - _dragOrigin;
            var next = new Point(_draggedNodeOrigin.X + delta.X / _zoom, _draggedNodeOrigin.Y + delta.Y / _zoom);
            _layoutCache[_draggedPackageId] = next;
            _customPositions[_draggedPackageId] = next;
            _nodeDragMoved |= Math.Abs(delta.X) > 2 || Math.Abs(delta.Y) > 2;
            _logicalBounds = CalculateLogicalBounds(_layoutCache.Values);
            Cursor = Cursors.SizeAll;
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (!_isPanning || e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed)
        {
            Cursor = ShowMinimap && _minimapBounds.Contains(current) ? Cursors.Cross : IsLayoutEditMode && HitTestNode(current) is not null ? Cursors.SizeAll : Cursors.Hand;
            UpdateHoveredPackage(ShowMinimap && _minimapBounds.Contains(current) ? null : HitTestNode(current));
            return;
        }
        UpdateHoveredPackage(null);
        _pan = new Point(_panOrigin.X + current.X - _dragOrigin.X, _panOrigin.Y + current.Y - _dragOrigin.Y);
        _hasInitializedView = true;
        InvalidateVisual();
    }


    private void UpdateHoveredPackage(string? packageId)
    {
        if (string.Equals(_hoveredPackageId, packageId, StringComparison.OrdinalIgnoreCase)) return;
        _hoveredPackageId = packageId;
        NodeHoverChanged?.Invoke(this, new ForgeGraphNodeHoverChangedEventArgs(packageId));
    }

    private bool TryBeginMinimapNavigation(Point point)
    {
        if (!ShowMinimap || _minimapBounds.IsEmpty || !_minimapBounds.Contains(point) || _minimapScale <= 0) return false;
        _isMinimapNavigating = true;
        Cursor = Cursors.SizeAll;
        NavigateFromMinimap(point);
        return true;
    }

    private void NavigateFromMinimap(Point point)
    {
        if (_minimapScale <= 0 || _logicalBounds.IsEmpty) return;
        var clamped = new Point(
            Math.Clamp(point.X, _minimapBounds.Left + 9, _minimapBounds.Right - 9),
            Math.Clamp(point.Y, _minimapBounds.Top + 9, _minimapBounds.Bottom - 9));
        var logical = new Point(
            _logicalBounds.X + (clamped.X - _minimapContentOrigin.X) / _minimapScale,
            _logicalBounds.Y + (clamped.Y - _minimapContentOrigin.Y) / _minimapScale);
        _pan = new Point(
            ActualWidth / 2 - logical.X * _zoom,
            ActualHeight / 2 - logical.Y * _zoom);
        _hasInitializedView = true;
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        var point = e.GetPosition(this);
        var hit = HitTestNode(point);
        if (!string.IsNullOrWhiteSpace(hit))
        {
            OpenNodeContextMenu(hit);
            e.Handled = true;
            return;
        }
        BeginPan(point);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning) { EndPan(); ReleaseMouseCapture(); e.Handled = true; }
    }

    private void OpenNodeContextMenu(string packageId)
    {
        var menu = new ContextMenu();
        var inspect = new MenuItem { Header = "Open in Mod Inspector" };
        inspect.Click += (_, _) => NodeInvoked?.Invoke(this, new ForgeGraphNodeInvokedEventArgs(packageId));
        var center = new MenuItem { Header = "Center node" };
        center.Click += (_, _) => CenterOnPackage(packageId);
        var focus = new MenuItem { Header = "Focus dependency path", IsCheckable = true, IsChecked = HighlightDependencyPaths };
        focus.Click += (_, _) => { HighlightDependencyPaths = focus.IsChecked; NodeInvoked?.Invoke(this, new ForgeGraphNodeInvokedEventArgs(packageId)); InvalidateVisual(); };
        menu.Items.Add(inspect);
        menu.Items.Add(center);
        menu.Items.Add(new Separator());
        menu.Items.Add(focus);
        menu.IsOpen = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Add:
            case Key.OemPlus: ZoomIn(); e.Handled = true; break;
            case Key.Subtract:
            case Key.OemMinus: ZoomOut(); e.Handled = true; break;
            case Key.F:
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) { FitToView(); e.Handled = true; }
                break;
            case Key.Home:
                if (CenterOnPackage(SelectedPackageId)) e.Handled = true;
                break;
            case Key.Left: _pan.X += 42; InvalidateVisual(); e.Handled = true; break;
            case Key.Right: _pan.X -= 42; InvalidateVisual(); e.Handled = true; break;
            case Key.Up: _pan.Y += 42; InvalidateVisual(); e.Handled = true; break;
            case Key.Down: _pan.Y -= 42; InvalidateVisual(); e.Handled = true; break;
        }
    }

    private void BeginPan(Point point) { _isPanning = true; _dragOrigin = point; _panOrigin = _pan; Cursor = Cursors.ScrollAll; }
    private void EndPan() { _isPanning = false; if (!_isMinimapNavigating) Cursor = Cursors.Hand; }

    private static IEnumerable<T> Enumerate<T>(IEnumerable? source) => source?.Cast<object>().OfType<T>() ?? Enumerable.Empty<T>();
    private static Color StatusColor(ModHealthStatus status) => status switch { ModHealthStatus.Healthy => Color.FromRgb(76,175,80), ModHealthStatus.Warning => Color.FromRgb(255,193,7), ModHealthStatus.Error => Color.FromRgb(217,83,79), ModHealthStatus.Updated => Color.FromRgb(77,163,255), _ => Color.FromRgb(120,124,132) };
    private void DrawCenteredText(DrawingContext dc, string value) { var text = MakeText(value, 14, Brushes.Gray, FontWeights.Normal); dc.DrawText(text, new Point(Math.Max(20, (ActualWidth-text.Width)/2), Math.Max(20,(ActualHeight-text.Height)/2))); }
    private void DrawText(DrawingContext dc, string value, double x, double y, double size, Brush brush, FontWeight weight, double width) { var text = MakeText(value, size, brush, weight); text.MaxTextWidth = Math.Max(1,width); text.Trimming = TextTrimming.CharacterEllipsis; dc.DrawText(text,new Point(x,y)); }
    private static FormattedText MakeText(string value,double size,Brush brush,FontWeight weight) => new(value ?? string.Empty,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,weight,FontStretches.Normal),size,brush,1.0);
}
