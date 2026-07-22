using System.IO;
using RimForge.Analysis.Services;
using RimForge.Core.BackgroundTasks;
using RimForge.Core.Diagnostics;
using RimForge.App.Commands;
using RimForge.App.Undo;
using RimForge.App.Lifecycle;
using RimForge.Core.Models;
using RimForge.Core.Services;
using RimForge.Infrastructure.Services;

namespace RimForge.App.Composition;

/// <summary>
/// Central construction point for application services.
/// Keeps concrete dependency wiring out of MainWindow while preserving
/// the existing service lifetimes and runtime behavior.
/// </summary>
public sealed class RimForgeApplicationServices : IAsyncDisposable
{
    private RimForgeApplicationServices(
        IApplicationEventBus eventBus,
        IApplicationLifecycleService lifecycleService,
        IDiagnosticService diagnosticService,
        IPlatformValidationService platformValidationService,
        IApplicationRecoveryService applicationRecoveryService,
        IStatePreservationService statePreservationService,
        ISignedUpdateService signedUpdateService,
        IModLibraryService modLibraryService,
        ILibraryProfileProjectionService libraryProfileProjectionService,
        IModAnalysisEngine analysisEngine,
        IForgeEvidenceService forgeEvidenceService,
        IForgeEvidenceBus forgeEvidenceBus,
        IForgeEvidenceRefreshScheduler forgeEvidenceRefreshScheduler,
        IForgeGraphProjectionService forgeGraphProjectionService,
        IForgeDnaService forgeDnaService,
        IDependencyIntelligenceService dependencyIntelligenceService,
        IDependencyManagementService dependencyManagementService,
        IProfileWorkspaceService profileWorkspaceService,
        IProfileEditService profileEditService,
        IExternalProfileReconciliationService externalProfileReconciliationService,
        IExternalProfileConflictService externalProfileConflictService,
        IModsConfigChangeMonitor modsConfigChangeMonitor,
        ISelectionService selectionService,
        IProfileWorkspaceStateService workspaceStateService,
        ISearchContext searchContext,
        INavigationContext navigationContext,
        IGlobalNavigationService globalNavigationService,
        IApplicationStatusService applicationStatusService,
        IUndoService undoService,
        IBackgroundTaskService backgroundTaskService,
        IHostedBackgroundWorkService hostedBackgroundWorkService,
        INotificationService notificationService,
        IRimForgeCommandRegistry commandRegistry,
        IModFilteringService modFilteringService,
        ISteamLibraryDiscoveryService steamLibraryDiscoveryService,
        IPlatformDiscoveryService platformDiscoveryService,
        IWorkspaceService workspaceService,
        IFeatureFlagService featureFlagService,
        IForgeSessionService forgeSessionService,
        ICompanionHost companionHost,
        IGameLogService gameLogService,
        IGameLaunchService gameLaunchService,
        ISteamStoreMetadataService steamStoreMetadataService,
        IRuntimeEvidenceStore runtimeEvidenceStore,
        ICompatibilityIntelligenceService compatibilityIntelligenceService,
        IRuntimeSensorHost runtimeSensorHost)
    {
        EventBus = eventBus;
        LifecycleService = lifecycleService;
        DiagnosticService = diagnosticService;
        PlatformValidationService = platformValidationService;
        ApplicationRecoveryService = applicationRecoveryService;
        StatePreservationService = statePreservationService;
        SignedUpdateService = signedUpdateService;
        ModLibraryService = modLibraryService;
        LibraryProfileProjectionService = libraryProfileProjectionService;
        AnalysisEngine = analysisEngine;
        ForgeEvidenceService = forgeEvidenceService;
        ForgeEvidenceBus = forgeEvidenceBus;
        ForgeEvidenceRefreshScheduler = forgeEvidenceRefreshScheduler;
        ForgeGraphProjectionService = forgeGraphProjectionService;
        ForgeDnaService = forgeDnaService;
        DependencyIntelligenceService = dependencyIntelligenceService;
        DependencyManagementService = dependencyManagementService;
        ProfileWorkspaceService = profileWorkspaceService;
        ProfileEditService = profileEditService;
        ExternalProfileReconciliationService = externalProfileReconciliationService;
        ExternalProfileConflictService = externalProfileConflictService;
        ModsConfigChangeMonitor = modsConfigChangeMonitor;
        SelectionService = selectionService;
        WorkspaceStateService = workspaceStateService;
        SearchContext = searchContext;
        NavigationContext = navigationContext;
        GlobalNavigationService = globalNavigationService;
        ApplicationStatusService = applicationStatusService;
        UndoService = undoService;
        BackgroundTaskService = backgroundTaskService;
        HostedBackgroundWorkService = hostedBackgroundWorkService;
        NotificationService = notificationService;
        CommandRegistry = commandRegistry;
        ModFilteringService = modFilteringService;
        SteamLibraryDiscoveryService = steamLibraryDiscoveryService;
        PlatformDiscoveryService = platformDiscoveryService;
        WorkspaceService = workspaceService;
        FeatureFlagService = featureFlagService;
        ForgeSessionService = forgeSessionService;
        CompanionHost = companionHost;
        GameLogService = gameLogService;
        GameLaunchService = gameLaunchService;
        SteamStoreMetadataService = steamStoreMetadataService;
        RuntimeEvidenceStore = runtimeEvidenceStore;
        CompatibilityIntelligenceService = compatibilityIntelligenceService;
        RuntimeSensorHost = runtimeSensorHost;
    }

    public IApplicationEventBus EventBus { get; }
    public IApplicationLifecycleService LifecycleService { get; }
    public IDiagnosticService DiagnosticService { get; }
    public IPlatformValidationService PlatformValidationService { get; }
    public IApplicationRecoveryService ApplicationRecoveryService { get; }
    public IStatePreservationService StatePreservationService { get; }
    public ISignedUpdateService SignedUpdateService { get; }
    public IModLibraryService ModLibraryService { get; }
    public ILibraryProfileProjectionService LibraryProfileProjectionService { get; }
    public IModAnalysisEngine AnalysisEngine { get; }
    public IForgeEvidenceService ForgeEvidenceService { get; }
    public IForgeEvidenceBus ForgeEvidenceBus { get; }
    public IForgeEvidenceRefreshScheduler ForgeEvidenceRefreshScheduler { get; }
    public IForgeGraphProjectionService ForgeGraphProjectionService { get; }
    public IForgeDnaService ForgeDnaService { get; }
    public IDependencyIntelligenceService DependencyIntelligenceService { get; }
    public IDependencyManagementService DependencyManagementService { get; }
    public IProfileWorkspaceService ProfileWorkspaceService { get; }
    public IProfileEditService ProfileEditService { get; }
    public IExternalProfileReconciliationService ExternalProfileReconciliationService { get; }
    public IExternalProfileConflictService ExternalProfileConflictService { get; }
    public IModsConfigChangeMonitor ModsConfigChangeMonitor { get; }
    public ISelectionService SelectionService { get; }
    public IProfileWorkspaceStateService WorkspaceStateService { get; }
    public ISearchContext SearchContext { get; }
    public INavigationContext NavigationContext { get; }
    public IGlobalNavigationService GlobalNavigationService { get; }
    public IApplicationStatusService ApplicationStatusService { get; }
    public IUndoService UndoService { get; }
    public IBackgroundTaskService BackgroundTaskService { get; }
    public IHostedBackgroundWorkService HostedBackgroundWorkService { get; }
    public INotificationService NotificationService { get; }
    public IRimForgeCommandRegistry CommandRegistry { get; }
    public IModFilteringService ModFilteringService { get; }
    public ISteamLibraryDiscoveryService SteamLibraryDiscoveryService { get; }
    public IPlatformDiscoveryService PlatformDiscoveryService { get; }
    public IWorkspaceService WorkspaceService { get; }
    public IFeatureFlagService FeatureFlagService { get; }
    public IForgeSessionService ForgeSessionService { get; }
    public ICompanionHost CompanionHost { get; }
    public IGameLogService GameLogService { get; }
    public IGameLaunchService GameLaunchService { get; }
    public ISteamStoreMetadataService SteamStoreMetadataService { get; }
    public IRuntimeEvidenceStore RuntimeEvidenceStore { get; }
    public ICompatibilityIntelligenceService CompatibilityIntelligenceService { get; }
    public IRuntimeSensorHost RuntimeSensorHost { get; }

    public static RimForgeApplicationServices CreateDefault()
    {
        var eventBus = new ApplicationEventBus();
        var lifecycleService = new ApplicationLifecycleService(eventBus);
        var configurationService = new JsonConfigurationService();
        var aboutXmlParser = new AboutXmlParser();
        var dependencyGraphService = new DependencyGraphService();
        var steamLibraryDiscoveryService = new SteamLibraryDiscoveryService();
        var gameLogService = new GameLogService();
        var repositoryRoot = RimForgePathLayout.ResolveRepositoryRoot();
        var paths = RimForgePathLayout.Create(repositoryRoot);
        var workspaceService = new WorkspaceService(paths);
        workspaceService.EnsureCreated();
        var platformDiscoveryService = new PlatformDiscoveryService(steamLibraryDiscoveryService, workspaceService);
        var sessionLog = new SessionLog(paths.SessionsRoot);
        var diagnosticService = new DiagnosticService(
            new JsonlLogSink(Path.Combine(paths.DiagnosticsRoot, "rimforge-diagnostics.jsonl")),
            sessionLog);
        diagnosticService.ReportHealth(new RuntimeHealth(
            HealthStatus.Healthy,
            "RimForge",
            "Shared diagnostics initialized.",
            DateTimeOffset.UtcNow));
        var platformValidationService = new PlatformValidationService(paths, diagnosticService);
        var applicationRecoveryService = new ApplicationRecoveryService(Path.Combine(paths.LocalApplicationDataRoot, "Recovery"));
        var statePreservationService = new StatePreservationService(paths);
        var signedUpdateService = new SignedUpdateService(Path.Combine(paths.LocalApplicationDataRoot, "Updates"), statePreservationService);

        var notificationService = new NotificationService(eventBus);
        var dependencyIntelligenceService = new DependencyIntelligenceService();
        var dependencyManagementService = new DependencyManagementService(dependencyIntelligenceService);
        var analysisEngine = new ModAnalysisEngine();
        var forgeDnaService = new ForgeDnaService(analysisEngine);
        var compatibilityIntelligenceService = new CompatibilityIntelligenceService();
        var runtimeEvidenceStore = new RuntimeEvidenceStore(
            Path.Combine(paths.CacheRoot, "RuntimeEvidence", "evidence-lake.json"),
            compatibilityIntelligenceService);
        var forgeEvidenceBus = new ForgeEvidenceBus();
        var forgeEvidenceService = new ForgeEvidenceService(
            new ForgeEvidenceStore(Path.Combine(paths.CacheRoot, "ForgeEvidence")),
            new ForgeEvidencePipeline(ForgeEvidenceProducerFactory.Create(runtimeEvidenceStore)),
            forgeEvidenceBus,
            ForgeEvidenceServiceOptions.Default);
        var forgeEvidenceRefreshScheduler = new ForgeEvidenceRefreshScheduler(forgeEvidenceService);
        var forgeGraphProjectionService = new ForgeGraphProjectionService(dependencyGraphService);
        var hostedBackgroundWorkService = new HostedBackgroundWorkService();
        var runtimeSensorHost = new RuntimeSensorHost(runtimeEvidenceStore, hostedBackgroundWorkService);
        var profileWorkspaceService = new ProfileWorkspaceService(platformDiscoveryService);

        return new RimForgeApplicationServices(
            eventBus,
            lifecycleService,
            diagnosticService,
            platformValidationService,
            applicationRecoveryService,
            statePreservationService,
            signedUpdateService,
            new ModLibraryService(
                configurationService,
                aboutXmlParser,
                dependencyGraphService,
                steamLibraryDiscoveryService),
            new LibraryProfileProjectionService(),
            analysisEngine,
            forgeEvidenceService,
            forgeEvidenceBus,
            forgeEvidenceRefreshScheduler,
            forgeGraphProjectionService,
            forgeDnaService,
            dependencyIntelligenceService,
            dependencyManagementService,
            profileWorkspaceService,
            new ProfileEditService(profileWorkspaceService),
            new ExternalProfileReconciliationService(),
            new ExternalProfileConflictService(profileWorkspaceService),
            new ModsConfigChangeMonitor(),
            new SelectionService(eventBus),
            new ProfileWorkspaceStateService(eventBus),
            new SearchContext(eventBus),
            new NavigationContext(eventBus),
            new GlobalNavigationService(),
            new ApplicationStatusService(),
            new UndoService(),
            new BackgroundTaskService(eventBus),
            hostedBackgroundWorkService,
            notificationService,
            new RimForgeCommandRegistry(),
            new ModFilteringService(),
            steamLibraryDiscoveryService,
            platformDiscoveryService,
            workspaceService,
            new FeatureFlagService(),
            new ForgeSessionService(eventBus, paths.SessionsRoot, diagnosticService, sessionLog),
            new CompanionHostService(diagnosticService),
            gameLogService,
            new GameLaunchService(gameLogService, platformDiscoveryService),
            new SteamStoreMetadataService(Path.Combine(paths.CacheRoot, "SteamContent")),
            runtimeEvidenceStore,
            compatibilityIntelligenceService,
            runtimeSensorHost);
    }


    private int _disposeState;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;

        LifecycleService.Transition(
            ApplicationLifecycleState.Stopping,
            "Shutdown",
            "Cancelling active work and disposing application services.");
        DiagnosticService.ReportHealth(new RuntimeHealth(
            HealthStatus.Degraded,
            "RimForge",
            "Application shutdown is in progress.",
            DateTimeOffset.UtcNow));

        BackgroundTaskService.CancelCurrent("Application shutdown requested.");
        if (ForgeSessionService.Current.Status is ForgeSessionStatus.Running or ForgeSessionStatus.Cancelling)
            ForgeSessionService.Cancel("Application shutdown requested.");

        try
        {
            ForgeEvidenceService.CancelCurrent();
            await ForgeEvidenceRefreshScheduler.DisposeAsync().ConfigureAwait(false);
            await ForgeEvidenceService.DisposeAsync().ConfigureAwait(false);

            await ModsConfigChangeMonitor.DisposeAsync().ConfigureAwait(false);

            await RuntimeSensorHost.StopAsync().ConfigureAwait(false);
            await RuntimeSensorHost.DisposeAsync().ConfigureAwait(false);
            await HostedBackgroundWorkService.DisposeAsync().ConfigureAwait(false);

            await CompanionHost.DisposeAsync().ConfigureAwait(false);

            await GameLogService.StopAsync().ConfigureAwait(false);
            await GameLogService.DisposeAsync().ConfigureAwait(false);

            if (ForgeSessionService is IDisposable forgeSessionService)
                forgeSessionService.Dispose();

            await ApplicationRecoveryService.CompleteRunAsync().ConfigureAwait(false);

            DiagnosticService.Dispose();

            NotificationService.Dispose();

            if (SteamStoreMetadataService is IAsyncDisposable asyncSteamMetadata)
                await asyncSteamMetadata.DisposeAsync().ConfigureAwait(false);
            else if (SteamStoreMetadataService is IDisposable steamMetadata)
                steamMetadata.Dispose();

            LifecycleService.Transition(
                ApplicationLifecycleState.Stopped,
                "Shutdown",
                "All composed application services were disposed.");

            if (EventBus is IDisposable eventBus)
                eventBus.Dispose();
        }
        catch (Exception ex)
        {
            LifecycleService.Transition(
                ApplicationLifecycleState.Failed,
                "Shutdown",
                "Application service disposal failed.",
                ex);
            throw;
        }
    }
}
