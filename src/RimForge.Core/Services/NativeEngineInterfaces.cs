using RimForge.Core.Models;

namespace RimForge.Core.Services;

public interface IConfigurationService
{
    Task<RimForgeConfiguration> LoadAsync(string repositoryRoot, CancellationToken cancellationToken = default);
}

public interface IAboutXmlParser
{
    Task<ModRecord> ParseAsync(string modFolder, CancellationToken cancellationToken = default);
}

public interface IDependencyGraphService
{
    (DependencyGraphModel Graph, IReadOnlyList<MissingDependency> Missing, IReadOnlyList<DependencyCycle> Cycles)
        Build(IReadOnlyList<ModRecord> mods);
}

public interface IModLibraryService
{
    Task<ModLibrarySnapshot> ScanAsync(
        string repositoryRoot,
        IProgress<ForgeProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<ModRecord>? discoveredModProgress = null,
        bool includeEvidence = true);

    Task<int> EnrichEvidenceAsync(
        string repositoryRoot,
        IReadOnlyList<ModRecord> mods,
        IProgress<ForgeProgress>? progress = null,
        IProgress<ModRecord>? enrichedModProgress = null,
        CancellationToken cancellationToken = default);
}

public interface ILibraryProfileProjectionService
{
    LibraryProfileWorkspaceSnapshot Create(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyList<RimForgeProfile> profiles,
        DateTimeOffset? generatedUtc = null);
}

public interface IProfileEditService
{
    ProfileEditDraft CreateDraft(
        LibraryProfileWorkspaceSnapshot workspace,
        string profileName,
        IReadOnlyList<string> proposedActiveMods);

    Task<ProfileEditCommitResult> CommitAsync(
        ProfileEditDraft draft,
        LibraryProfileWorkspaceSnapshot currentWorkspace,
        CancellationToken cancellationToken = default);
}

public interface IProfileCatalogStateStore
{
    ProfileCatalogState Load(string profilesRoot);
    ProfileCatalogState Save(string profilesRoot, ProfileCatalogState state);
}

public interface IProfileWorkspaceService
{
    Task<IReadOnlyList<RimForgeProfile>> LoadProfilesAsync(
        string repositoryRoot,
        IReadOnlyList<ModRecord> installedMods,
        CancellationToken cancellationToken = default);

    Task<ProfileActivationResult> ActivateAsync(
        RimForgeProfile profile,
        CancellationToken cancellationToken = default);

    Task<ProfileActivationResult> LaunchAsync(
        RimForgeProfile profile,
        CancellationToken cancellationToken = default);

    Task<ProfileActivationResult> RestoreActivationRecoveryAsync(
        string recoveryPath,
        CancellationToken cancellationToken = default);

    Task<LoadOrderSaveResult> SaveLoadOrderAsync(
        RimForgeProfile profile,
        IReadOnlyList<string> activeMods,
        CancellationToken cancellationToken = default);

    Task<ProfileOperationResult> CreateAsync(
        string repositoryRoot,
        string name,
        IReadOnlyList<string>? activeMods = null,
        string version = "1.6",
        CancellationToken cancellationToken = default);

    Task<ProfileOperationResult> DuplicateAsync(
        string repositoryRoot,
        RimForgeProfile source,
        string newName,
        CancellationToken cancellationToken = default);

    Task<ProfileOperationResult> RenameAsync(
        string repositoryRoot,
        RimForgeProfile profile,
        string newName,
        CancellationToken cancellationToken = default);

    Task<ProfileOperationResult> DeleteAsync(
        string repositoryRoot,
        RimForgeProfile profile,
        CancellationToken cancellationToken = default);

    Task<ProfileOperationResult> ImportAsync(
        string repositoryRoot,
        string sourcePath,
        string profileName,
        CancellationToken cancellationToken = default);

    Task<ProfileOperationResult> ExportAsync(
        RimForgeProfile profile,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task<ProfileOperationResult> RestoreAsync(
        string repositoryRoot,
        string backupPath,
        string? profileName = null,
        CancellationToken cancellationToken = default);

    ProfileComparisonResult Compare(RimForgeProfile left, RimForgeProfile right);

    string GetRimWorldModsConfigPath();
}

public interface ISteamLibraryService
{
    IReadOnlyList<SteamInstallationCandidate> FindRimWorldInstallations(
        IEnumerable<string>? additionalRoots = null);
}

public interface ISteamLibraryDiscoveryService : ISteamLibraryService;

public interface IRimWorldInstallationService
{
    IReadOnlyList<SteamInstallationCandidate> FindInstallations(
        IEnumerable<string>? additionalSteamRoots = null);
}

public interface IWorkspaceService
{
    RimForgePathLayout Paths { get; }
    void EnsureCreated();
}

public interface IPlatformDiscoveryService
{
    PlatformDiscoverySnapshot Discover(IEnumerable<string>? additionalSteamRoots = null);
}

public interface IDependencyIntelligenceService
{
    DependencyIntelligenceReport Analyze(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyCollection<string> activePackageIds,
        string? selectedPackageId);
}

public interface IDependencyManagementService
{
    DependencyActivationPlan PlanActivation(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyCollection<string> activePackageIds,
        IReadOnlyCollection<string> requestedPackageIds);

    DependencyRemovalPlan PlanRemoval(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyCollection<string> activePackageIds,
        IReadOnlyCollection<string> requestedPackageIds);

    IReadOnlyList<DependencyReason> FindOrphans(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyCollection<string> activePackageIds);

    DependencyManagementSummary Summarize(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyCollection<string> activePackageIds);
}

public interface IExternalProfileReconciliationService
{
    Task<ExternalProfileSnapshot> ReadAsync(string modsConfigPath, CancellationToken cancellationToken = default);

    ExternalProfileReconciliation Compare(RimForgeProfile profile, ExternalProfileSnapshot external);
}

public interface IExternalProfileConflictService
{
    Task<ExternalProfileResolutionResult> ResolveAsync(
        ExternalProfileReconciliation reconciliation,
        ExternalProfileResolution resolution,
        CancellationToken cancellationToken = default);
}

public interface IModsConfigChangeMonitor : IAsyncDisposable
{
    event EventHandler<ExternalProfileChange>? Changed;

    string? WatchedPath { get; }

    Task StartAsync(string modsConfigPath, CancellationToken cancellationToken = default);

    Task AcknowledgeCurrentAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}
