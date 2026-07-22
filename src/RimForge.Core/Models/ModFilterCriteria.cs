namespace RimForge.Core.Models;

public sealed record ModFilterCriteria(
    string SearchText,
    bool ShowFullLibrary,
    bool IssuesOnly,
    IReadOnlySet<string>? ActivePackageIds = null,
    IReadOnlyList<ModRecord>? Library = null,
    StructuredSearchQuery? Query = null);
