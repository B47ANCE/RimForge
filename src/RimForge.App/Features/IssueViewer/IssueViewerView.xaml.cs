using System;
using System.Windows;
using System.Windows.Controls;
using RimForge.Analysis.Models;

namespace RimForge.App.Features.IssueViewer;

public sealed class IssueModNavigationRequestedEventArgs : EventArgs
{
    public IssueModNavigationRequestedEventArgs(string packageId, bool openForgeView)
    {
        PackageId = packageId;
        OpenForgeView = openForgeView;
    }

    public string PackageId { get; }
    public bool OpenForgeView { get; }
}

public partial class IssueViewerView : UserControl
{
    public static readonly RoutedEvent FixSelectedRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(FixSelectedRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(IssueViewerView));

    public static readonly RoutedEvent FixAllRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(FixAllRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(IssueViewerView));

    public IssueViewerView() => InitializeComponent();

    public event EventHandler<IssueModNavigationRequestedEventArgs>? ModNavigationRequested;

    public event RoutedEventHandler FixSelectedRequested
    {
        add => AddHandler(FixSelectedRequestedEvent, value);
        remove => RemoveHandler(FixSelectedRequestedEvent, value);
    }

    public event RoutedEventHandler FixAllRequested
    {
        add => AddHandler(FixAllRequestedEvent, value);
        remove => RemoveHandler(FixAllRequestedEvent, value);
    }

    public ListBox ItemsList => IssueList;

    public void FocusIssue(IssueWorkItem issue)
    {
        IssueList.SelectedItem = issue;
        IssueList.UpdateLayout();
        IssueList.ScrollIntoView(issue);
    }

    private void FixSelectedIssue_Click(object sender, RoutedEventArgs e)
    {
        var args = new RoutedEventArgs(FixSelectedRequestedEvent, sender);
        RaiseEvent(args);
    }

    private void FixAllIssues_Click(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(FixAllRequestedEvent, this));

    private void InspectMod_Click(object sender, RoutedEventArgs e) =>
        RaiseModNavigation(sender, openForgeView: false);

    private void OpenInForgeView_Click(object sender, RoutedEventArgs e) =>
        RaiseModNavigation(sender, openForgeView: true);

    private void RelatedMod_Click(object sender, RoutedEventArgs e) =>
        RaiseModNavigation(sender, openForgeView: false);

    private void RaiseModNavigation(object sender, bool openForgeView)
    {
        if (sender is Button { Tag: string packageId } && !string.IsNullOrWhiteSpace(packageId))
            ModNavigationRequested?.Invoke(this, new IssueModNavigationRequestedEventArgs(packageId, openForgeView));
    }

    public event RoutedEventHandler? ToggleIgnoreRequested;

    private void ToggleIgnore_Click(object sender, RoutedEventArgs e) =>
        ToggleIgnoreRequested?.Invoke(sender, e);
}
