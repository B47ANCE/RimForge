namespace RimForge.Core.Services;

public interface ISearchContext
{
    string QueryText { get; }
    event EventHandler<string>? QueryChanged;
    void SetQuery(string? queryText);
    void Clear();
}

public interface INavigationContext
{
    string? CurrentPackageId { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    event EventHandler? NavigationChanged;
    void Record(string? packageId);
    string? GoBack();
    string? GoForward();
    void Clear();
}
