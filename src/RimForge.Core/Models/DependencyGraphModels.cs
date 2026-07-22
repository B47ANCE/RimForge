namespace RimForge.Core.Models;

public enum DependencyRelationshipType { Required, Optional, LoadBefore, LoadAfter, Incompatible, PatchTarget }
public enum ModHealthStatus { Healthy, Warning, Error, Unknown, Updated }

public sealed record DependencyGraphNode(
    string Id,
    string Name,
    string? PackageId,
    string? Author,
    string? WorkshopId,
    string? WorkshopUrl,
    string? LocalPath,
    IReadOnlyList<string> SupportedVersions,
    DateTimeOffset? LastUpdated,
    ModHealthStatus Status);

public sealed record DependencyGraphEdge(
    string SourceId,
    string TargetId,
    DependencyRelationshipType Relationship,
    string Description,
    int DeclarationCount = 1,
    IReadOnlyList<string>? DeclarationSources = null,
    ForgeGraphRelationshipProvenance? Provenance = null);

public sealed record DependencyGraphModel(
    IReadOnlyList<DependencyGraphNode> Nodes,
    IReadOnlyList<DependencyGraphEdge> Edges);
