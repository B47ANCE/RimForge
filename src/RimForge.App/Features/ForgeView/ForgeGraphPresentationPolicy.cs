using RimForge.Core.Models;

namespace RimForge.App.Features.ForgeView;

/// <summary>
/// Applies display-only graph rules without altering the authoritative dependency evidence.
/// </summary>
internal static class ForgeGraphPresentationPolicy
{
    public static bool ShouldDisplayEdge(DependencyGraphEdge edge)
    {
        var sourceIsCore = LoadOrderRules.IsCore(edge.SourceId);
        var targetIsCore = LoadOrderRules.IsCore(edge.TargetId);
        if (!sourceIsCore && !targetIsCore) return true;

        var otherPackageId = sourceIsCore ? edge.TargetId : edge.SourceId;
        return LoadOrderRules.IsOfficialDlc(otherPackageId);
    }
}
