using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RimForge.App.Features.Search;

namespace RimForge.App.Features.CommandBar;

public partial class EngineeringCommandBarView : UserControl
{
    private bool _suppressSearchReopen;

    public EngineeringCommandBarView() => InitializeComponent();

    public event RoutedEventHandler? BackRequested;
    public event RoutedEventHandler? ForwardRequested;
    public event RoutedEventHandler? UndoRequested;
    public event RoutedEventHandler? ReforgeRequested;
    public event EventHandler<string>? NavigationRequested;
    public event EventHandler<SearchDiscoveryResult>? SearchResultInvoked;

    public void FocusGlobalSearch()
    {
        GlobalSearchBox.Focus();
        Keyboard.Focus(GlobalSearchBox);
        GlobalSearchBox.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (GlobalSearchBox.IsKeyboardFocusWithin)
                    GlobalSearchBox.SelectAll();
            }));
    }


    private void GlobalSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && SearchResultsList.Items.Count > 0)
        {
            SearchResultsPopup.SetCurrentValue(Popup.IsOpenProperty, true);
            SearchResultsList.SelectedIndex = Math.Max(0, SearchResultsList.SelectedIndex);
            SearchResultsList.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && SearchResultsList.Items.Count > 0)
        {
            SearchResultsList.SelectedIndex = Math.Max(0, SearchResultsList.SelectedIndex);
            InvokeSelectedSearchResult();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(GlobalSearchBox.Text))
                ClearSearch();
            else
                CloseSearchResults(returnFocus: false);
            e.Handled = true;
        }
    }

    private void GlobalSearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        ReopenSearchResultsIfNeeded();

    private void GlobalSearchBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (GlobalSearchBox.IsKeyboardFocusWithin) return;
        GlobalSearchBox.SelectionLength = 0;
    }

    private void GlobalSearchBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        ReopenSearchResultsIfNeeded();

    private void SearchClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearSearch();
        GlobalSearchBox.Focus();
        Keyboard.Focus(GlobalSearchBox);
    }

    private void ClearSearch()
    {
        GlobalSearchBox.Clear();
        GlobalSearchBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        SearchResultsPopup.SetCurrentValue(Popup.IsOpenProperty, false);
    }

    private void ReopenSearchResultsIfNeeded()
    {
        if (!_suppressSearchReopen && !string.IsNullOrWhiteSpace(GlobalSearchBox.Text))
            SearchResultsPopup.SetCurrentValue(Popup.IsOpenProperty, true);
    }

    private void SearchResultsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { InvokeSelectedSearchResult(); e.Handled = true; }
        else if (e.Key == Key.Escape)
        {
            ClearSearch();
            GlobalSearchBox.Focus();
            e.Handled = true;
        }
    }

    private void SearchResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (SearchResultsList.SelectedItem is null) return;
        InvokeSelectedSearchResult();
        e.Handled = true;
    }

    private void SearchResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => InvokeSelectedSearchResult();

    private void InvokeSelectedSearchResult()
    {
        if (SearchResultsList.SelectedItem is not SearchDiscoveryResult result) return;
        CloseSearchResults(returnFocus: false);
        SearchResultInvoked?.Invoke(this, result);
    }

    private void CloseSearchResults(bool returnFocus)
    {
        _suppressSearchReopen = true;
        SearchResultsPopup.SetCurrentValue(Popup.IsOpenProperty, false);
        if (returnFocus) GlobalSearchBox.Focus();
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => _suppressSearchReopen = false));
    }

    private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, e);
    private void Forward_Click(object sender, RoutedEventArgs e) => ForwardRequested?.Invoke(this, e);
    private void Reforge_Click(object sender, RoutedEventArgs e) => ReforgeRequested?.Invoke(this, e);
    private void Undo_Click(object sender, RoutedEventArgs e) => UndoRequested?.Invoke(this, e);

    private void NavigationMenu_Click(object sender, RoutedEventArgs e) =>
        NavigationMenuPopup.IsOpen = !NavigationMenuPopup.IsOpen;

    private void NavigationDestination_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string destination }) return;
        NavigationMenuPopup.IsOpen = false;
        NavigationRequested?.Invoke(this, destination);
    }

    public void SetLocation(string destination)
    {
        var buttons = new[]
        {
            ModSorterNavigationButton,
            IssueViewerNavigationButton,
            ForgeViewNavigationButton,
            TextureToolsNavigationButton,
            ConsoleNavigationButton,
            SettingsNavigationButton
        };
        foreach (var button in buttons)
        {
            button.Background = Brushes.Transparent;
            button.BorderBrush = Brushes.Transparent;
            button.Foreground = (Brush)FindResource("TextMutedBrush");
        }

        var active = buttons.FirstOrDefault(button =>
            string.Equals(button.Tag?.ToString(), destination, StringComparison.OrdinalIgnoreCase));
        if (active is null) return;
        active.Background = (Brush)FindResource("NavSelectedBrush");
        active.BorderBrush = (Brush)FindResource("AccentBrush");
        active.Foreground = (Brush)FindResource("TextPrimaryBrush");
    }
}
