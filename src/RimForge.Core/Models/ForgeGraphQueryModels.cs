namespace RimForge.Core.Models;

public enum ForgeGraphQueryOrigin
{
    Canvas,
    Outline,
    Search,
    IssueNavigation,
    Inspector,
    History
}

public sealed record ForgeGraphRelationshipProvenance(
    string SourceKind,
    string SourceId,
    IReadOnlyList<string> EvidenceIds,
    string Summary)
{
    public static ForgeGraphRelationshipProvenance FromDeclaration(DependencyGraphEdge edge)
    {
        var sources = (edge.DeclarationSources ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ForgeGraphRelationshipProvenance(
            "ModMetadata",
            sources.FirstOrDefault() ?? "dependency-manifest",
            sources,
            string.IsNullOrWhiteSpace(edge.Description) ? $"{edge.Relationship} declaration" : edge.Description);
    }
}

public sealed record ForgeGraphQuery(
    IReadOnlyCollection<string>? SearchPackageIds = null,
    bool SearchActive = false,
    IReadOnlyCollection<string>? ProfilePackageIds = null,
    bool ShowFullLibrary = true,
    ModHealthStatus? Health = null,
    IReadOnlyCollection<DependencyRelationshipType>? Relationships = null,
    string? FocusPackageId = null,
    bool IsolateFocusedPath = false)
{
    public IReadOnlyCollection<string> EffectiveSearchPackageIds => SearchPackageIds ?? Array.Empty<string>();
    public IReadOnlyCollection<string> EffectiveProfilePackageIds => ProfilePackageIds ?? Array.Empty<string>();
    public IReadOnlyCollection<DependencyRelationshipType> EffectiveRelationships => Relationships ?? Array.Empty<DependencyRelationshipType>();
}

public sealed record ForgeGraphQueryResult(
    IReadOnlyList<DependencyGraphNode> Nodes,
    IReadOnlyList<DependencyGraphEdge> Edges,
    int TotalNodes,
    int TotalEdges);

public sealed record ForgeGraphSelectionSnapshot(
    string? SelectedPackageId,
    string? FocusedPackageId,
    ForgeGraphQueryOrigin Origin,
    IReadOnlyList<string> History,
    int HistoryIndex);
