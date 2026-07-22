using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RimForge.App.Features.ModSorter;

internal sealed class ModDragPreviewAdorner : Adorner
{
    private readonly Border _preview;
    private readonly VisualCollection _visuals;
    private Point _position;

    public ModDragPreviewAdorner(UIElement adornedElement, IReadOnlyList<string> names) : base(adornedElement)
    {
        // Initialize the visual collection before changing dependency properties.
        // WPF may query VisualChildrenCount from the IsHitTestVisible change callback.
        _visuals = new VisualCollection(this);
        var summary = names.Count == 1
            ? names[0]
            : $"{names.Count} mods\n{string.Join(" • ", names.Take(3))}{(names.Count > 3 ? " • …" : string.Empty)}";
        _preview = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(238, 47, 49, 54)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(242, 140, 40)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Opacity = 0.96,
            Child = new TextBlock
            {
                Text = summary,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 320
            }
        };
        _visuals.Add(_preview);
        IsHitTestVisible = false;
    }

    public void MoveTo(Point point)
    {
        _position = new Point(point.X + 18, point.Y + 18);
        InvalidateArrange();
    }

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override Size MeasureOverride(Size constraint)
    {
        _preview.Measure(new Size(340, double.PositiveInfinity));
        return AdornedElement.RenderSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var desired = _preview.DesiredSize;
        var x = Math.Min(_position.X, Math.Max(0, finalSize.Width - desired.Width - 4));
        var y = Math.Min(_position.Y, Math.Max(0, finalSize.Height - desired.Height - 4));
        _preview.Arrange(new Rect(new Point(Math.Max(0, x), Math.Max(0, y)), desired));
        return finalSize;
    }
}

internal sealed class ModDropInsertionAdorner : Adorner
{
    private readonly Pen _pen = new(new SolidColorBrush(Color.FromRgb(242, 140, 40)), 2);
    private double _y;

    public ModDropInsertionAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    public void SetPosition(double y)
    {
        _y = y;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var width = AdornedElement.RenderSize.Width;
        drawingContext.DrawLine(_pen, new Point(4, _y), new Point(Math.Max(4, width - 4), _y));
        drawingContext.DrawEllipse(_pen.Brush, null, new Point(5, _y), 3, 3);
        drawingContext.DrawEllipse(_pen.Brush, null, new Point(Math.Max(5, width - 5), _y), 3, 3);
    }
}
