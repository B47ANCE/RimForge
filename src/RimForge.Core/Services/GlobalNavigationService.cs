namespace RimForge.Core.Services;

public sealed record WorkstationNavigationSnapshot(
    string PageTitle,
    string? SelectedPackageId,
    string SearchText,
    string? SelectedProfileName);

public interface IGlobalNavigationService
{
    WorkstationNavigationSnapshot? Current { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    event EventHandler? StateChanged;
    void Record(WorkstationNavigationSnapshot snapshot);
    WorkstationNavigationSnapshot? GoBack();
    WorkstationNavigationSnapshot? GoForward();
    void Clear();
}

public sealed class GlobalNavigationService : IGlobalNavigationService
{
    private const int MaximumHistoryLength = 100;
    private readonly List<WorkstationNavigationSnapshot> _history = new();
    private int _index = -1;
    private bool _isRestoring;

    public WorkstationNavigationSnapshot? Current =>
        _index >= 0 && _index < _history.Count ? _history[_index] : null;
    public bool CanGoBack => _index > 0;
    public bool CanGoForward => _index >= 0 && _index < _history.Count - 1;
    public event EventHandler? StateChanged;

    public void Record(WorkstationNavigationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (_isRestoring || snapshot == Current) return;
        if (_index < _history.Count - 1)
            _history.RemoveRange(_index + 1, _history.Count - _index - 1);
        _history.Add(snapshot);
        if (_history.Count > MaximumHistoryLength) _history.RemoveAt(0);
        _index = _history.Count - 1;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public WorkstationNavigationSnapshot? GoBack() => Move(-1);
    public WorkstationNavigationSnapshot? GoForward() => Move(1);

    public void Clear()
    {
        _history.Clear();
        _index = -1;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private WorkstationNavigationSnapshot? Move(int offset)
    {
        var target = _index + offset;
        if (target < 0 || target >= _history.Count) return null;
        _isRestoring = true;
        try
        {
            _index = target;
            return _history[_index];
        }
        finally
        {
            _isRestoring = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
