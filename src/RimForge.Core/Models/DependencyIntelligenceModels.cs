namespace RimForge.Core.Models;

public enum DependencyConfidenceLevel { Unknown, Low, Medium, High, Verified }

public sealed record DependencyReason(
    string PackageId,
    string DisplayName,
    IReadOnlyList<string> Path,
    string Explanation,
    DependencyConfidenceLevel Confidence,
    int ConfidencePercent);

public sealed record DependencyIntelligenceReport(
    string PackageId,
    IReadOnlyList<DependencyReason> DirectDependencies,
    IReadOnlyList<DependencyReason> TransitiveDependencies,
    IReadOnlyList<DependencyReason> DirectDependents,
    IReadOnlyList<DependencyReason> TransitiveDependents,
    IReadOnlyList<DependencyReason> RemovalImpact,
    IReadOnlyList<DependencyReason> OrphanCandidates,
    bool IsOrphan,
    int ConfidencePercent,
    string Summary,
    string WhyEnabled,
    string RemovalExplanation)
{
    public static DependencyIntelligenceReport Empty(string packageId = "") => new(
        packageId,
        Array.Empty<DependencyReason>(),
        Array.Empty<DependencyReason>(),
        Array.Empty<DependencyReason>(),
        Array.Empty<DependencyReason>(),
        Array.Empty<DependencyReason>(),
        Array.Empty<DependencyReason>(),
        false,
        0,
        "No dependency intelligence is available.",
        "No active dependency path explains this mod.",
        "No active mods are affected by removing this mod.");
}
