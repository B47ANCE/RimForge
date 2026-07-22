namespace RimForge.Core.Services;

public sealed class NavigationContext : INavigationContext
{
    private readonly IApplicationEventBus? _eventBus;
    private const int MaximumHistoryLength = 50;
    private readonly List<string> _history = new();
    private int _index = -1;
    private bool _isNavigating;

    public NavigationContext(IApplicationEventBus? eventBus = null) => _eventBus = eventBus;

    public string? CurrentPackageId => _index >= 0 && _index < _history.Count ? _history[_index] : null;
    public bool CanGoBack => _index > 0;
    public bool CanGoForward => _index >= 0 && _index < _history.Count - 1;
    public event EventHandler? NavigationChanged;

    public void Record(string? packageId)
    {
        if (_isNavigating || string.IsNullOrWhiteSpace(packageId)) return;
        if (string.Equals(CurrentPackageId, packageId, StringComparison.OrdinalIgnoreCase)) return;

        if (_index < _history.Count - 1)
            _history.RemoveRange(_index + 1, _history.Count - _index - 1);

        _history.Add(packageId);
        if (_history.Count > MaximumHistoryLength) _history.RemoveAt(0);
        _index = _history.Count - 1;
        PublishChanged();
    }

    public string? GoBack() => Move(-1);
    public string? GoForward() => Move(1);

    public void Clear()
    {
        _history.Clear();
        _index = -1;
        PublishChanged();
    }

    private void PublishChanged()
    {
        NavigationChanged?.Invoke(this, EventArgs.Empty);
        _eventBus?.Publish(new NavigationChangedEvent(CurrentPackageId, CanGoBack, CanGoForward));
    }

    private string? Move(int offset)
    {
        var target = _index + offset;
        if (target < 0 || target >= _history.Count) return null;
        _isNavigating = true;
        try
        {
            _index = target;
            return _history[_index];
        }
        finally
        {
            _isNavigating = false;
            PublishChanged();
        }
    }
}
