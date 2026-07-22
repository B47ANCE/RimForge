using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RimForge.UI.Controls;

public enum ForgeIconKind
{
    Add,
    Rename,
    Duplicate,
    Favorite,
    Import,
    Export,
    Lock,
    Unlock,
    Delete,
    Refresh,
    OpenFolder,
    Launch,
    Ignite,
    Inspector,
    Repair,
    Profile
}

/// <summary>
/// Central vector icon host for RimForge. All geometry is DPI-independent and tintable.
/// </summary>
public sealed class ForgeIcon : Viewbox
{
    private static readonly IReadOnlyDictionary<ForgeIconKind, Geometry> Geometries =
        new Dictionary<ForgeIconKind, Geometry>
        {
            [ForgeIconKind.Add] = Geometry.Parse("M8,1 L8,15 M1,8 L15,8"),
            [ForgeIconKind.Rename] = Geometry.Parse("M2,12 L2,15 L5,15 L14,6 L10,2 Z M9,3 L13,7"),
            [ForgeIconKind.Duplicate] = Geometry.Parse("M5,5 L15,5 L15,15 L5,15 Z M1,1 L11,1 L11,5 M1,1 L1,11 L5,11"),
            [ForgeIconKind.Favorite] = Geometry.Parse("M8,1 L10.1,5.3 L15,6 L11.5,9.3 L12.4,14 L8,11.7 L3.6,14 L4.5,9.3 L1,6 L5.9,5.3 Z"),
            [ForgeIconKind.Import] = Geometry.Parse("M8,1 L8,10 M4,6 L8,10 L12,6 M2,12 L2,15 L14,15 L14,12"),
            [ForgeIconKind.Export] = Geometry.Parse("M8,15 L8,6 M4,10 L8,6 L12,10 M2,4 L2,1 L14,1 L14,4"),
            [ForgeIconKind.Lock] = Geometry.Parse("M3,7 L13,7 L13,15 L3,15 Z M5,7 L5,5 A3,3 0 0 1 11,5 L11,7"),
            [ForgeIconKind.Unlock] = Geometry.Parse("M3,7 L13,7 L13,15 L3,15 Z M6,7 L6,5 A3,3 0 0 1 12,5"),
            [ForgeIconKind.Delete] = Geometry.Parse("M3,4 L13,4 M6,1 L10,1 L11,4 M4,4 L5,15 L11,15 L12,4 M7,7 L7,12 M9,7 L9,12"),
            [ForgeIconKind.Refresh] = Geometry.Parse("M3.2,6.2 C4.1,3.7 6.8,2.2 9.5,2.8 C11.2,3.2 12.6,4.3 13.4,5.8 M13.4,5.8 L13.4,2.4 M13.4,5.8 L10,5.8 M12.8,9.8 C11.9,12.3 9.2,13.8 6.5,13.2 C4.8,12.8 3.4,11.7 2.6,10.2 M2.6,10.2 L2.6,13.6 M2.6,10.2 L6,10.2"),
            [ForgeIconKind.OpenFolder] = Geometry.Parse("M1,4 L6,4 L8,6 L15,6 L13,14 L2,14 Z M1,4 L1,13"),
            [ForgeIconKind.Launch] = Geometry.Parse("M2,14 L7,9 M7,9 L5,5 L14,1 L10,10 Z M5,11 L2,11 L2,14 L5,14"),
            [ForgeIconKind.Ignite] = Geometry.Parse("M8,1 C11,5 12,7 10,9 C10,6 8,5 7,3 C7,7 3,8 3,12 C3,15 5,16 8,16 C12,16 14,14 14,11 C14,7 11,4 8,1 Z"),
            [ForgeIconKind.Inspector] = Geometry.Parse("M8,1 A7,7 0 1 1 8,15 A7,7 0 1 1 8,1 M8,7 L8,12 M8,4 L8,5"),
            [ForgeIconKind.Repair] = Geometry.Parse("M10.8,1.5 C9.2,1.1 7.5,1.6 6.4,2.8 C5.2,4.1 5,5.9 5.7,7.4 L1.5,11.6 C0.8,12.3 0.8,13.5 1.5,14.2 C2.2,14.9 3.4,14.9 4.1,14.2 L8.3,10 C9.8,10.7 11.6,10.5 12.9,9.3 C14.1,8.2 14.6,6.5 14.2,4.9 L11.3,7.8 L8.2,7.1 L7.5,4 Z"),
            [ForgeIconKind.Profile] = Geometry.Parse("M8,2 A3,3 0 1 1 8,8 A3,3 0 1 1 8,2 M2,15 C2,11 5,9 8,9 C11,9 14,11 14,15")
        };

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ForgeIconKind), typeof(ForgeIcon),
        new FrameworkPropertyMetadata(ForgeIconKind.Profile, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
        nameof(IconBrush), typeof(Brush), typeof(ForgeIcon),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(ForgeIcon),
        new FrameworkPropertyMetadata(1.6, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    private readonly Path _path;

    public ForgeIcon()
    {
        Width = 16;
        Height = 16;
        Stretch = Stretch.Uniform;
        _path = new Path
        {
            Fill = Brushes.Transparent,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.Uniform
        };
        Child = _path;
        UpdateVisual();
    }

    public ForgeIconKind Kind
    {
        get => (ForgeIconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Brush IconBrush
    {
        get => (Brush)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ForgeIcon)d).UpdateVisual();

    private void UpdateVisual()
    {
        if (_path is null)
        {
            return;
        }

        _path.Data = Geometries[Kind];
        _path.Stroke = IconBrush;
        _path.StrokeThickness = StrokeThickness;
    }
}
