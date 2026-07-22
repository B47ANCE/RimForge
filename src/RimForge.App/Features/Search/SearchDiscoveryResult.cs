using System.Windows.Media;

namespace RimForge.App.Features.Search;

public enum SearchDiscoveryKind
{
    Mod,
    Issue,
    Workspace
}

public sealed record SearchDiscoveryResult(
    SearchDiscoveryKind Kind,
    string Title,
    string Subtitle,
    string TargetId,
    string Glyph,
    int Score,
    Geometry? SourceIconGeometry = null,
    string SourceToolTip = "",
    string HealthState = "Pending",
    string HealthToolTip = "")
{
    public bool ShowModIdentity => Kind == SearchDiscoveryKind.Mod;
    public bool ShowKindGlyph => !ShowModIdentity;
    public bool IsFeature => Kind == SearchDiscoveryKind.Workspace;
}
