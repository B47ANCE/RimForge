namespace RimForge.Core.Models;

public sealed class FeatureFlags
{
    public bool AnalysisInspectorV2 { get; init; } = true;
    public bool ProfileHealthScore { get; init; } = true;
    public bool RepairRecommendations { get; init; } = true;
    public bool AdvancedForgeView { get; init; } = true;
    public bool ExperimentalAutoSort { get; init; }
}
