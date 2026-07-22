using System;
using System.Windows;
using System.Windows.Controls;

namespace RimForge.App.Features.ModInspector;

public sealed class RelatedModRequestedEventArgs : EventArgs
{
    public RelatedModRequestedEventArgs(string packageId) => PackageId = packageId;
    public string PackageId { get; }
}

public partial class ModInspectorView : UserControl
{
    public static readonly RoutedEvent ToggleRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(ToggleRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ModInspectorView));

    public static readonly RoutedEvent OpenFolderRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(OpenFolderRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ModInspectorView));

    public static readonly RoutedEvent OpenBrowserRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(OpenBrowserRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ModInspectorView));

    public static readonly RoutedEvent SelectionBackRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(SelectionBackRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ModInspectorView));

    public static readonly RoutedEvent SelectionForwardRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(SelectionForwardRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ModInspectorView));

    public static readonly RoutedEvent OpenSteamRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(OpenSteamRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ModInspectorView));

    public ModInspectorView() => InitializeComponent();

    public event EventHandler<RelatedModRequestedEventArgs>? RelatedModRequested;

    public event RoutedEventHandler ToggleRequested
    {
        add => AddHandler(ToggleRequestedEvent, value);
        remove => RemoveHandler(ToggleRequestedEvent, value);
    }

    public event RoutedEventHandler OpenFolderRequested
    {
        add => AddHandler(OpenFolderRequestedEvent, value);
        remove => RemoveHandler(OpenFolderRequestedEvent, value);
    }

    public event RoutedEventHandler OpenBrowserRequested
    {
        add => AddHandler(OpenBrowserRequestedEvent, value);
        remove => RemoveHandler(OpenBrowserRequestedEvent, value);
    }


    public event RoutedEventHandler SelectionBackRequested
    {
        add => AddHandler(SelectionBackRequestedEvent, value);
        remove => RemoveHandler(SelectionBackRequestedEvent, value);
    }

    public event RoutedEventHandler SelectionForwardRequested
    {
        add => AddHandler(SelectionForwardRequestedEvent, value);
        remove => RemoveHandler(SelectionForwardRequestedEvent, value);
    }

    public event RoutedEventHandler OpenSteamRequested
    {
        add => AddHandler(OpenSteamRequestedEvent, value);
        remove => RemoveHandler(OpenSteamRequestedEvent, value);
    }

    public void SetExpanded(bool expanded)
    {
        InspectorExpandedContent.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        InspectorCollapsedRail.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(ToggleRequestedEvent, this));

    private void SelectionBackButton_Click(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(SelectionBackRequestedEvent, this));

    private void SelectionForwardButton_Click(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(SelectionForwardRequestedEvent, this));

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(OpenFolderRequestedEvent, this));

    private void OpenBrowserButton_Click(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(OpenBrowserRequestedEvent, this));

    private void OpenSteamButton_Click(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(OpenSteamRequestedEvent, this));

    private void RelatedModButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string packageId } && !string.IsNullOrWhiteSpace(packageId))
            RelatedModRequested?.Invoke(this, new RelatedModRequestedEventArgs(packageId));
    }
}
