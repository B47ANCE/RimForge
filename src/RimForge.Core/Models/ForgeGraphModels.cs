namespace RimForge.Core.Models;

public sealed record ForgeGraphEvidenceEntry(
    string ModId,
    string PackageId,
    string Fingerprint,
    ModEvidence Evidence);

public sealed record ForgeGraphEvidenceInput(
    int Generation,
    IReadOnlyList<ForgeGraphEvidenceEntry> Entries,
    IReadOnlyList<ForgeEvidenceContribution> Contributions);

public sealed record ForgeGraphProjectionMetrics(
    int EvidenceGeneration,
    int Nodes,
    int Edges,
    int ReusedNodes,
    int RebuiltNodes,
    TimeSpan Elapsed,
    string TopologySignature);

public sealed record ForgeGraphDiff(
    IReadOnlyList<string> AddedNodes,
    IReadOnlyList<string> RemovedNodes,
    IReadOnlyList<string> ChangedNodes,
    IReadOnlyList<string> AddedEdges,
    IReadOnlyList<string> RemovedEdges);

public sealed record ForgeGraphCluster(
    string Id,
    IReadOnlyList<string> Members,
    bool IsCycle);

public sealed record ForgeGraphIntelligenceMetrics(
    int RequiredEdges,
    int OrderingEdges,
    int ConflictEdges,
    int DependentsIndexed,
    int StronglyConnectedComponents,
    int CyclicComponents,
    TimeSpan Elapsed);

public sealed record ForgeGraphIntelligence(
    IReadOnlyDictionary<string, IReadOnlyList<string>> Dependents,
    IReadOnlyList<ForgeGraphCluster> Clusters,
    ForgeGraphDiff Diff,
    ForgeGraphIntelligenceMetrics Metrics);

public sealed record ForgeGraphProjection(
    DependencyGraphModel Graph,
    IReadOnlyList<MissingDependency> MissingDependencies,
    IReadOnlyList<DependencyCycle> Cycles,
    ForgeGraphIntelligence Intelligence,
    ForgeGraphProjectionMetrics Metrics);
