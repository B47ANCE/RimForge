using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using RimForge.UI.ViewModels;

namespace RimForge.App.Features.ModSorter;

public partial class ModSorterView : UserControl
{
    private ModDragPreviewAdorner? _dragPreview;
    private AdornerLayer? _dragPreviewLayer;
    private ModDropInsertionAdorner? _dropIndicator;
    private AdornerLayer? _dropIndicatorLayer;
    private ListBox? _dropIndicatorList;

    public ModSorterView() => InitializeComponent();

    public event EventHandler<ModSorterSelectionEventArgs>? SelectionRequested;
    public event EventHandler<ModSorterMouseButtonEventArgs>? DragStartRequested;
    public event EventHandler<ModSorterMouseEventArgs>? DragMoveRequested;
    public event EventHandler<ModSorterDragEventArgs>? ActiveDropRequested;
    public event EventHandler<ModSorterDragEventArgs>? InactiveDropRequested;
    public event RoutedEventHandler? SearchSteamLibrariesRequested;
    public event RoutedEventHandler? OpenSettingsRequested;
    public event EventHandler<ModHealthNavigationRequestedEventArgs>? HealthNavigationRequested;
    public event RoutedEventHandler? EnableSelectedRequested;
    public event RoutedEventHandler? DisableSelectedRequested;

    public ListBox ActiveList => ActiveModsList;
    public ListBox InactiveList => InactiveModsList;
    public FrameworkElement EmptyState => LibraryEmptyState;

    public void BeginDragVisual(IReadOnlyList<ProfileLoadOrderItemViewModel> items)
    {
        EndDragVisual();
        _dragPreviewLayer = AdornerLayer.GetAdornerLayer(this);
        if (_dragPreviewLayer is null) return;
        _dragPreview = new ModDragPreviewAdorner(this, items.Select(item => item.DisplayName).ToArray());
        _dragPreviewLayer.Add(_dragPreview);
        _dragPreview.MoveTo(Mouse.GetPosition(this));
    }

    public void EndDragVisual()
    {
        ClearDropIndicator();
        if (_dragPreview is not null && _dragPreviewLayer is not null)
            _dragPreviewLayer.Remove(_dragPreview);
        _dragPreview = null;
        _dragPreviewLayer = null;
    }

    public void SelectItems(ListBox list, IReadOnlyCollection<ProfileLoadOrderItemViewModel> items)
    {
        var packageIds = items
            .Select(item => item.PackageId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dispatcher.BeginInvoke(() =>
        {
            list.SelectedItems.Clear();
            var selected = list.Items
                .Cast<ProfileLoadOrderItemViewModel>()
                .Where(item => packageIds.Contains(item.PackageId))
                .ToArray();
            foreach (var item in selected) list.SelectedItems.Add(item);
            if (selected.Length > 0) list.ScrollIntoView(selected[0]);
        });
    }

    public int GetInsertionIndex(ListBox list, DragEventArgs e)
    {
        var position = e.GetPosition(list);
        var container = FindContainer(list, position);
        if (container is null) return list.Items.Count;
        var index = list.ItemContainerGenerator.IndexFromContainer(container);
        var local = e.GetPosition(container);
        return local.Y > container.ActualHeight / 2 ? index + 1 : index;
    }

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SelectionRequested?.Invoke(this, new ModSorterSelectionEventArgs((ListBox)sender, e));

    private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        DragStartRequested?.Invoke(this, new ModSorterMouseButtonEventArgs((ListBox)sender, e));

    private void List_PreviewMouseMove(object sender, MouseEventArgs e) =>
        DragMoveRequested?.Invoke(this, new ModSorterMouseEventArgs((ListBox)sender, e));

    private void Active_Drop(object sender, DragEventArgs e)
    {
        ClearDropIndicator();
        ActiveDropRequested?.Invoke(this, new ModSorterDragEventArgs((ListBox)sender, e));
    }

    private void Inactive_Drop(object sender, DragEventArgs e)
    {
        ClearDropIndicator();
        InactiveDropRequested?.Invoke(this, new ModSorterDragEventArgs((ListBox)sender, e));
    }

    private void List_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (sender is not ListBox list || !e.Data.GetDataPresent(typeof(ModDragPayload)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        UpdateDropIndicator(list, e);
        _dragPreview?.MoveTo(e.GetPosition(this));
        e.Handled = true;
    }

    private void List_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is ListBox list && ReferenceEquals(list, _dropIndicatorList))
            ClearDropIndicator();
    }

    private void List_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        _dragPreview?.MoveTo(Mouse.GetPosition(this));
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private void List_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        if (e.EscapePressed)
        {
            e.Action = DragAction.Cancel;
            EndDragVisual();
            e.Handled = true;
        }
    }

    private void UpdateDropIndicator(ListBox list, DragEventArgs e)
    {
        if (!ReferenceEquals(_dropIndicatorList, list))
        {
            ClearDropIndicator();
            _dropIndicatorList = list;
            _dropIndicatorLayer = AdornerLayer.GetAdornerLayer(list);
            if (_dropIndicatorLayer is not null)
            {
                _dropIndicator = new ModDropInsertionAdorner(list);
                _dropIndicatorLayer.Add(_dropIndicator);
            }
        }

        if (_dropIndicator is null) return;
        var position = e.GetPosition(list);
        var container = FindContainer(list, position);
        var y = container is null
            ? Math.Max(2, list.ActualHeight - 4)
            : container.TranslatePoint(
                new Point(0, e.GetPosition(container).Y > container.ActualHeight / 2 ? container.ActualHeight : 0),
                list).Y;
        _dropIndicator.SetPosition(Math.Clamp(y, 2, Math.Max(2, list.ActualHeight - 2)));
    }

    private void ClearDropIndicator()
    {
        if (_dropIndicator is not null && _dropIndicatorLayer is not null)
            _dropIndicatorLayer.Remove(_dropIndicator);
        _dropIndicator = null;
        _dropIndicatorLayer = null;
        _dropIndicatorList = null;
    }

    private static ListBoxItem? FindContainer(ListBox list, Point position)
    {
        var element = list.InputHitTest(position) as DependencyObject;
        while (element is not null && element is not ListBoxItem)
            element = VisualTreeHelper.GetParent(element);
        return element as ListBoxItem;
    }

    private void HealthAnvil_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ProfileLoadOrderItemViewModel item })
        {
            HealthNavigationRequested?.Invoke(this, new ModHealthNavigationRequestedEventArgs(item));
            e.Handled = true;
        }
    }

    private void SearchSteamLibraries_Click(object sender, RoutedEventArgs e) => SearchSteamLibrariesRequested?.Invoke(this, e);
    private void OpenSettings_Click(object sender, RoutedEventArgs e) => OpenSettingsRequested?.Invoke(this, e);
    private void EnableSelected_Click(object sender, RoutedEventArgs e) => EnableSelectedRequested?.Invoke(this, e);
    private void DisableSelected_Click(object sender, RoutedEventArgs e) => DisableSelectedRequested?.Invoke(this, e);
}

public sealed record ModDragPayload(IReadOnlyList<ProfileLoadOrderItemViewModel> Items, bool FromActive);
public sealed record ModSorterSelectionEventArgs(ListBox List, SelectionChangedEventArgs Original);
public sealed record ModSorterMouseButtonEventArgs(ListBox List, MouseButtonEventArgs Original);
public sealed record ModSorterMouseEventArgs(ListBox List, MouseEventArgs Original);
public sealed record ModSorterDragEventArgs(ListBox List, DragEventArgs Original);

public sealed record ModHealthNavigationRequestedEventArgs(ProfileLoadOrderItemViewModel Item);
