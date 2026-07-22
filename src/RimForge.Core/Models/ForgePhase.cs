namespace RimForge.Core.Models;

public enum ForgePhase
{
    Idle = 0,
    Configuration,
    Discovery,
    AboutParsing,
    Validation,
    IndexBuilding,
    DependencyGraph,
    EvidenceScan,
    VersionChecks,
    ProfileProcessing,
    ReportGeneration,
    DatabaseGeneration,
    Complete,
    Cancelled,
    Error
}
