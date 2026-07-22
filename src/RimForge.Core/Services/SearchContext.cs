namespace RimForge.Core.Services;

public sealed class SearchContext : ISearchContext
{
    private readonly IApplicationEventBus? _eventBus;
    private string _queryText = string.Empty;

    public SearchContext(IApplicationEventBus? eventBus = null) => _eventBus = eventBus;

    public string QueryText => _queryText;
    public event EventHandler<string>? QueryChanged;

    public void SetQuery(string? queryText)
    {
        var normalized = queryText ?? string.Empty;
        if (string.Equals(_queryText, normalized, StringComparison.Ordinal)) return;
        _queryText = normalized;
        QueryChanged?.Invoke(this, _queryText);
        _eventBus?.Publish(new SearchQueryChangedEvent(_queryText));
    }

    public void Clear() => SetQuery(string.Empty);
}
