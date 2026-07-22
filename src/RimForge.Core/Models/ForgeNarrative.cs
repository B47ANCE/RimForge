namespace RimForge.Core.Models;

public static class ForgeNarrative
{
    public static string For(ForgePhase phase) => phase switch
    {
        ForgePhase.Configuration => "Laying out the tools...",
        ForgePhase.Discovery => "Gathering raw materials...",
        ForgePhase.AboutParsing => "Studying the blueprints...",
        ForgePhase.Validation => "Inspecting the materials...",
        ForgePhase.IndexBuilding => "Preparing the anvil...",
        ForgePhase.DependencyGraph => "Heating the steel...",
        ForgePhase.EvidenceScan => "Hammering out imperfections...",
        ForgePhase.VersionChecks => "Tempering the edge...",
        ForgePhase.ProfileProcessing => "Fitting the components...",
        ForgePhase.ReportGeneration => "Polishing the finish...",
        ForgePhase.DatabaseGeneration => "Marking the maker's seal...",
        ForgePhase.Complete => "Forge complete.",
        ForgePhase.Cancelled => "The forge has gone quiet.",
        ForgePhase.Error => "The forge needs attention.",
        _ => "The forge is ready."
    };

    public static string PurposeFor(ForgePhase phase) => phase switch
    {
        ForgePhase.Configuration => "Loading configuration and resolving workspace paths.",
        ForgePhase.Discovery => "Discovering installed mods and official content.",
        ForgePhase.AboutParsing => "Collecting dependency, version, path, and assembly evidence.",
        ForgePhase.Validation => "Validating metadata and package identity.",
        ForgePhase.IndexBuilding => "Building searchable indexes for the current installation.",
        ForgePhase.DependencyGraph => "Building dependency and reverse-dependency relationships.",
        ForgePhase.EvidenceScan => "Collecting compatibility and conflict evidence.",
        ForgePhase.VersionChecks => "Checking supported RimWorld versions and external metadata.",
        ForgePhase.ProfileProcessing => "Applying the selected profile and its active load order.",
        ForgePhase.ReportGeneration => "Generating actionable audit and health results.",
        ForgePhase.DatabaseGeneration => "Writing reusable analysis data for future sessions.",
        ForgePhase.Complete => "The selected profile is ready to review or launch.",
        ForgePhase.Cancelled => "The current operation stopped without changing the active profile.",
        ForgePhase.Error => "Review the Console for the failure details and recovery actions.",
        _ => "Preparing analysis context."
    };
}
