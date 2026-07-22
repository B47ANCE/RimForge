using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using RimForge.Analysis.Models;
using RimForge.App.Serialization;
using RimForge.App.Collections;
using RimForge.App.Startup;
using RimForge.App.Composition;
using RimForge.App.Commands;
using RimForge.App.Undo;
using RimForge.App.Lifecycle;
using RimForge.Core.BackgroundTasks;
using RimForge.Analysis.Services;
using RimForge.Core.Diagnostics;
using RimForge.Core.Models;
using RimForge.Core.Services;
using RimForge.Infrastructure.Services;
using RimForge.App.Forge;
using RimForge.UI.ViewModels;
using RimForge.UI.Presentation;
using RimForge.UI.Dialogs;

namespace RimForge.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly RimForgeApplicationServices _applicationServices;
    private readonly IApplicationLifecycleService _lifecycleService;
    private readonly IDiagnosticService _diagnosticService;
    private readonly NativeForgeRunner _nativeForgeRunner;
    private readonly IModLibraryService _modLibraryService;
    private readonly IModAnalysisEngine _analysisEngine;
    private readonly IForgeEvidenceService _forgeEvidenceService;
    private readonly IForgeEvidenceBus _forgeEvidenceBus;
    private readonly IForgeEvidenceQueryService _forgeEvidenceQueryService = new ForgeEvidenceQueryService();
    private readonly IForgeEvidenceRefreshScheduler _forgeEvidenceRefreshScheduler;
    private readonly IForgeGraphProjectionService _forgeGraphProjectionService;
    private readonly IForgeDnaService _forgeDnaService;
    private readonly IDependencyIntelligenceService _dependencyIntelligenceService;
    private readonly IDependencyManagementService _dependencyManagementService;
    private readonly IssueEngine _issueEngine = new();
    private readonly RepairPlanner _repairPlanner = new();
    private readonly IProfileWorkspaceService _profileWorkspaceService;
    private readonly IProfileCatalogStateStore _profileCatalogStateStore;
    private readonly IProfilePackageInspectionService _profilePackageInspectionService;
    private readonly IExternalProfileReconciliationService _externalProfileReconciliationService;
    private readonly IExternalProfileConflictService _externalProfileConflictService;
    private readonly IModsConfigChangeMonitor _modsConfigChangeMonitor;
    private readonly IApplicationEventBus _eventBus;
    private readonly List<IDisposable> _eventSubscriptions = new();
    private readonly ISelectionService _selectionService;
    private readonly IProfileWorkspaceStateService _workspaceStateService;
    private readonly ISearchContext _searchContext;
    private readonly INavigationContext _navigationContext;
    private readonly IGlobalNavigationService _globalNavigationService;
    private readonly IApplicationStatusService _applicationStatusService;
    private readonly IUndoService _undoService;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly INotificationService _notificationService;
    private readonly IRimForgeCommandRegistry _commandRegistry;
    private readonly IModFilteringService _modFilteringService;
    private readonly ISteamLibraryDiscoveryService _steamLibraryDiscoveryService;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IForgeSessionService _forgeSessionService;
    private readonly ICompanionHost _companionHost;
    private readonly IGameLogService _gameLogService;
    private readonly IGameLaunchService _gameLaunchService;
    private readonly ISteamStoreMetadataService _steamStoreMetadataService;
    private readonly System.Windows.Threading.DispatcherTimer _forgeElapsedTimer;
    private readonly Dictionary<Popup, DispatcherTimer> _evidencePopupCloseTimers = new();
    private readonly HashSet<Popup> _pinnedEvidencePopups = new();
    private readonly HashSet<Popup> _moveEnabledEvidencePopups = new();
    private readonly HashSet<Popup> _knownEvidencePopups = new();
    private Popup? _activeTransientEvidencePopup;
    private Popup? _draggedEvidencePopup;
    private FrameworkElement? _evidenceDragCapture;
    private Point _evidenceDragStart;
    private double _evidenceDragStartHorizontalOffset;
    private double _evidenceDragStartVerticalOffset;
    private readonly StringBuilder _log = new();
    private string _pageTitle = "Mod Sorter";
    private string _statusText = "Ready";
    private Brush _statusBrush = Brushes.Gray;
    private AuditSummary _summary = AuditSummary.Empty;
    private ModRecord? _selectedMod;
    private SteamStoreMetadata? _selectedSteamContentMetadata;
    private bool _isSelectedSteamContentLoading;
    private string _selectedSteamContentStatusText = string.Empty;
    private RimForgeProfile? _selectedProfile;
    private string _nativeProgressText = "Waiting";
    private int _nativeProgressValue;
    private int _graphNodeCount;
    private int _graphEdgeCount;
    private string _forgeNarrativeText = ForgeNarrative.For(ForgePhase.Idle);
    private string _forgeTechnicalMessage = "Ready to inspect the current modpack ecosystem.";
    private string _forgePurposeText = "Purpose: Preparing analysis context.";
    private int _overallProgressValue;
    private int _phaseProgressValue;
    private bool _isPhaseIndeterminate;
    private bool _isForgeAttentionRequired;
    private string _forgeAttentionMessage = string.Empty;
    private ForgePhase _currentForgePhase = ForgePhase.Idle;
    private string _workshopFolder = string.Empty;
    private string _localModsFolder = string.Empty;
    private string _outputFolderSetting = "Output";
    private int _externalTimeoutSeconds = 10;
    private bool _showForgeNarrative = true;
    private bool _openConsoleOnGameLaunch;
    private string _targetRimWorldVersion = "1.6";
    private bool _showFullLibrary;
    private bool _showIssuesOnly = true;
    private ModSorterItemViewModel? _selectedSorterItem;
    private string _settingsStatus = string.Empty;
    private ModAnalysisSnapshot? _analysisSnapshot;
    private ForgeEvidenceSnapshot _forgeEvidenceSnapshot = ForgeEvidenceSnapshot.Empty;
    private DependencyIntelligenceReport _dependencyIntelligence = DependencyIntelligenceReport.Empty();
    private string? _pendingDependencyRemovalNotificationId;
    private string[] _pendingDependencyRemovalPackageIds = Array.Empty<string>();
    private string? _pendingOrphanCleanupNotificationId;
    private string[] _pendingOrphanCleanupPackageIds = Array.Empty<string>();
    private string? _pendingProfileBackupPath;
    private string? _pendingProfileExportPath;
    private string? _pendingActivationRecoveryPath;
    private ExternalProfileReconciliation? _pendingExternalProfileReconciliation;
    private IssueViewerSnapshot? _issueViewerSnapshot;
    private SteamInstallationCandidate? _selectedSteamInstallation;
    private ForgeSessionSnapshot _forgeSession = ForgeSessionSnapshot.Idle;
    private CompanionHostProcessSnapshot _companionHostState = CompanionHostProcessSnapshot.Stopped;
    private RuntimeHealth _runtimeHealth = RuntimeHealth.Unknown("RimForge");
    private BackgroundTaskSnapshot _backgroundTask = BackgroundTaskSnapshot.Idle;
    private NotificationSnapshot _notificationState = NotificationSnapshot.Empty;
    private bool _isLoadOrderDirty;
    private bool _isInstantAutoSortEnabled = true;
    private DependencyAssistanceMode _dependencyAssistanceMode = DependencyAssistanceMode.Automatic;
    private OrphanCleanupMode _orphanCleanupMode = OrphanCleanupMode.Ask;
    private ProfileLoadOrderItemViewModel? _selectedLoadOrderItem;
    private Point _loadOrderDragStart;
    private RimForgeProfile? _forgedProfile;
    private readonly HashSet<string> _favoriteProfileNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lockedProfileNames = new(StringComparer.OrdinalIgnoreCase);
    private bool _isFirstRunGuideVisible;
    private bool _isNativeScanVisible;
    private bool _isNativeScanComplete;
    private bool _isNativeScanSuccessful;
    private bool _acceptPreliminaryDiscovery;
    private const int CurrentFirstRunGuideRevision = 2;
    private bool _firstRunGuideCompleted;
    private int _firstRunGuideRevision;
    private CancellationTokenSource? _forgeStatusHideCts;
    private NativeLibraryCacheMetrics? _lastNativeLibraryCacheMetrics;
    private StartupUiProjectionMetrics? _lastStartupUiProjectionMetrics;
    private long _preliminaryProjectionTicks;
    private int _preliminaryProjectionCount;
    private bool _startupStarted;
    private bool _isClosing;
    private long _startupCoordinatorStartedTicks;
    private long _firstRenderTicks;
    private Task? _backgroundIntelligenceTask;
    private Task? _analysisRefreshTask;
    private int _analysisRefreshVersion;

    private void ForgeEvidenceBadge_MouseEnter(object sender, MouseEventArgs e)
    {
        if (TryGetEvidencePopup(sender, out var popup))
        {
            _knownEvidencePopups.Add(popup);
            CancelEvidencePopupClose(popup);
            ActivateTransientEvidencePopup(popup);
            popup.IsOpen = true;
        }
    }

    private void ForgeEvidenceBadge_MouseLeave(object sender, MouseEventArgs e)
    {
        if (TryGetEvidencePopup(sender, out var popup))
        {
            ScheduleEvidencePopupClose(popup);
        }
    }

    private void ForgeEvidenceBadge_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!TryGetEvidencePopup(sender, out var popup)) return;

        _knownEvidencePopups.Add(popup);
        if (_pinnedEvidencePopups.Contains(popup))
        {
            _pinnedEvidencePopups.Remove(popup);
            _moveEnabledEvidencePopups.Remove(popup);
            ActivateTransientEvidencePopup(popup);
            popup.IsOpen = true;
        }
        else
        {
            _pinnedEvidencePopups.Add(popup);
            if (ReferenceEquals(_activeTransientEvidencePopup, popup))
            {
                _activeTransientEvidencePopup = null;
            }
            CancelEvidencePopupClose(popup);
            popup.IsOpen = true;
        }

        e.Handled = true;
    }

    private void ForgeEvidencePopup_MouseEnter(object sender, MouseEventArgs e)
    {
        if (TryGetEvidencePopup(sender, out var popup))
        {
            CancelEvidencePopupClose(popup);
        }
    }

    private void ForgeEvidencePopup_MouseLeave(object sender, MouseEventArgs e)
    {
        if (TryGetEvidencePopup(sender, out var popup))
        {
            ScheduleEvidencePopupClose(popup);
        }
    }

    private void ForgeEvidencePopup_PinClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetEvidencePopup(sender, out var popup)) return;

        if (!_pinnedEvidencePopups.Add(popup))
        {
            _pinnedEvidencePopups.Remove(popup);
            _moveEnabledEvidencePopups.Remove(popup);
            ActivateTransientEvidencePopup(popup);
        }
        else if (ReferenceEquals(_activeTransientEvidencePopup, popup))
        {
            _activeTransientEvidencePopup = null;
        }

        CancelEvidencePopupClose(popup);
        popup.IsOpen = true;
        e.Handled = true;
    }

    private void ForgeEvidencePopup_MoveClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetEvidencePopup(sender, out var popup)) return;

        _pinnedEvidencePopups.Add(popup);
        if (ReferenceEquals(_activeTransientEvidencePopup, popup))
        {
            _activeTransientEvidencePopup = null;
        }

        var moveEnabled = _moveEnabledEvidencePopups.Add(popup);
        if (!moveEnabled)
        {
            _moveEnabledEvidencePopups.Remove(popup);
        }

        if (sender is Button moveButton)
        {
            moveButton.Content = moveEnabled ? "MOVE ✓" : "MOVE";
        }

        CancelEvidencePopupClose(popup);
        popup.IsOpen = true;
        e.Handled = true;
    }

    private void ForgeEvidencePopup_HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement header || !TryGetEvidencePopup(sender, out var popup)) return;
        if (!_pinnedEvidencePopups.Contains(popup) || !_moveEnabledEvidencePopups.Contains(popup)) return;
        if (FindVisualAncestor<Button>(e.OriginalSource as DependencyObject) is not null) return;

        _draggedEvidencePopup = popup;
        _evidenceDragCapture = header;
        _evidenceDragStart = e.GetPosition(this);
        _evidenceDragStartHorizontalOffset = popup.HorizontalOffset;
        _evidenceDragStartVerticalOffset = popup.VerticalOffset;
        header.CaptureMouse();
        header.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void ForgeEvidencePopup_HeaderMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedEvidencePopup is null || _evidenceDragCapture is null || e.LeftButton != MouseButtonState.Pressed) return;

        var current = e.GetPosition(this);
        _draggedEvidencePopup.HorizontalOffset = _evidenceDragStartHorizontalOffset + current.X - _evidenceDragStart.X;
        _draggedEvidencePopup.VerticalOffset = _evidenceDragStartVerticalOffset + current.Y - _evidenceDragStart.Y;
        e.Handled = true;
    }

    private void ForgeEvidencePopup_HeaderMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_evidenceDragCapture is null) return;

        _evidenceDragCapture.ReleaseMouseCapture();
        _evidenceDragCapture.Cursor = Cursors.Arrow;
        _evidenceDragCapture = null;
        _draggedEvidencePopup = null;
        e.Handled = true;
    }

    private static T? FindVisualAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match) return match;
        }

        return null;
    }

    private void ForgeEvidencePopup_CloseClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetEvidencePopup(sender, out var popup)) return;

        _pinnedEvidencePopups.Remove(popup);
        _moveEnabledEvidencePopups.Remove(popup);
        if (ReferenceEquals(_activeTransientEvidencePopup, popup))
        {
            _activeTransientEvidencePopup = null;
        }
        CancelEvidencePopupClose(popup);
        popup.IsOpen = false;
        e.Handled = true;
    }

    private void ForgeEvidencePopup_CopyFilesClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ModEvidenceBadge badge)
        {
            var fileList = string.Join(Environment.NewLine, badge.FileList);
            if (!string.IsNullOrWhiteSpace(fileList))
            {
                Clipboard.SetText(fileList);
                StatusText = $"Copied {badge.FileList.Count:N0} evidence file path(s).";
            }
        }

        if (TryGetEvidencePopup(sender, out var popup))
        {
            _pinnedEvidencePopups.Add(popup);
            if (ReferenceEquals(_activeTransientEvidencePopup, popup))
            {
                _activeTransientEvidencePopup = null;
            }
            CancelEvidencePopupClose(popup);
            popup.IsOpen = true;
        }

        e.Handled = true;
    }

    private void ActivateTransientEvidencePopup(Popup popup)
    {
        if (_pinnedEvidencePopups.Contains(popup)) return;

        if (_activeTransientEvidencePopup is not null &&
            !ReferenceEquals(_activeTransientEvidencePopup, popup) &&
            !_pinnedEvidencePopups.Contains(_activeTransientEvidencePopup))
        {
            CancelEvidencePopupClose(_activeTransientEvidencePopup);
            _activeTransientEvidencePopup.IsOpen = false;
        }

        _activeTransientEvidencePopup = popup;
    }

    private bool TryGetEvidencePopup(object sender, out Popup popup)
    {
        if (sender is FrameworkElement element && element.Tag is Popup taggedPopup)
        {
            popup = taggedPopup;
            _knownEvidencePopups.Add(popup);
            return true;
        }

        if (sender is DependencyObject source)
        {
            foreach (var candidate in _knownEvidencePopups)
            {
                if (candidate.Child is DependencyObject child && IsVisualDescendant(child, source))
                {
                    popup = candidate;
                    return true;
                }
            }
        }

        popup = null!;
        return false;
    }

    private static bool IsVisualDescendant(DependencyObject root, DependencyObject candidate)
    {
        for (DependencyObject? current = candidate; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, root)) return true;
        }

        return false;
    }

    private void ScheduleEvidencePopupClose(Popup popup)
    {
        if (_pinnedEvidencePopups.Contains(popup)) return;

        if (!_evidencePopupCloseTimers.TryGetValue(popup, out var timer))
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (!_pinnedEvidencePopups.Contains(popup))
                {
                    popup.IsOpen = false;
                    if (ReferenceEquals(_activeTransientEvidencePopup, popup))
                    {
                        _activeTransientEvidencePopup = null;
                    }
                }
            };
            _evidencePopupCloseTimers[popup] = timer;
        }

        timer.Stop();
        timer.Start();
    }

    private void CancelEvidencePopupClose(Popup popup)
    {
        if (_evidencePopupCloseTimers.TryGetValue(popup, out var timer))
        {
            timer.Stop();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public BulkObservableCollection<ModRecord> Mods { get; } = new();
    public BulkObservableCollection<ModSorterItemViewModel> ModSorterItems { get; } = new();
    public BulkObservableCollection<IssueWorkItem> IssueItems { get; } = new();
    public BulkObservableCollection<DependencyGraphNode> DependencyNodes { get; } = new();
    public BulkObservableCollection<DependencyGraphEdge> DependencyEdges { get; } = new();
    public ObservableCollection<ActivityEntry> ActivityEntries { get; } = new();
    public BulkObservableCollection<RimForgeProfile> Profiles { get; } = new();
    public BulkObservableCollection<ProfileLoadOrderItemViewModel> ProfileLoadOrderItems { get; } = new();
    public BulkObservableCollection<ProfileLoadOrderItemViewModel> ActiveProfileMods { get; } = new();
    public BulkObservableCollection<ProfileLoadOrderItemViewModel> InactiveInstalledMods { get; } = new();
    public ObservableCollection<GameLogEntry> GameLogEntries { get; } = new();
    private bool _isLoadingOlderGameLogEntries;
    private bool _gameLogAutoFollow = true;
    public ICollectionView ModsView { get; }
    public ICollectionView ModSorterView { get; }
    public ICollectionView IssueItemsView { get; }
    public ICollectionView ActiveProfileModsView { get; }
    public ICollectionView InactiveInstalledModsView { get; }
    public ICollectionView DependencyEdgesView { get; }

    public string PageTitle
    {
        get => _pageTitle;
        set
        {
            if (Set(ref _pageTitle, value))
            {
                Notify(nameof(CommandBarStatusDetail));
                Notify(nameof(CommandBarSearchSummaryText));
            }
        }
    }
    public string StatusText
    {
        get => _statusText;
        set
        {
            if (Set(ref _statusText, value))
            {
                ProjectManualApplicationStatus(value);
                Notify(nameof(CommandBarStatusText));
                Notify(nameof(CommandBarStatusDetail));
            }
        }
    }
    public Brush StatusBrush
    {
        get => _statusBrush;
        set
        {
            if (Set(ref _statusBrush, value)) Notify(nameof(CommandBarStatusBrush));
        }
    }
    public string CommandBarStatusText => _applicationStatusService?.Current.Label ?? StatusText;
    public string CommandBarStatusDetail => _applicationStatusService?.Current.Detail ?? PageTitle;
    public Brush CommandBarStatusBrush => (_applicationStatusService?.Current.Kind ?? ApplicationStatusKind.Ready) switch
    {
        ApplicationStatusKind.Ready => (Brush)FindResource("SuccessBrush"),
        ApplicationStatusKind.Error => (Brush)FindResource("DangerBrush"),
        ApplicationStatusKind.Cancelling => (Brush)FindResource("WarningBrush"),
        _ => (Brush)FindResource("WarningBrush")
    };
    public bool CanUndo => _undoService?.CanUndo == true;
    public string UndoPreviewText => CanUndo
        ? $"Undo: {_undoService.PendingDescription}"
        : "Nothing to undo.";
    public string CommandBarSearchSummaryText => HasSearchValidationError
        ? SearchValidationText
        : IsSearchActive ? SearchSummaryText : PageTitle;
    public AuditSummary Summary { get => _summary; set => Set(ref _summary, value); }
    public string LogText => _log.ToString();
    public string NativeProgressText { get => _nativeProgressText; set => Set(ref _nativeProgressText, value); }
    public int NativeProgressValue { get => _nativeProgressValue; set => Set(ref _nativeProgressValue, value); }
    public bool IsFirstRunGuideVisible { get => _isFirstRunGuideVisible; private set => Set(ref _isFirstRunGuideVisible, value); }
    public bool IsNativeScanVisible { get => _isNativeScanVisible; private set => Set(ref _isNativeScanVisible, value); }
    public bool IsNativeScanComplete { get => _isNativeScanComplete; private set => Set(ref _isNativeScanComplete, value); }
    public bool IsNativeScanSuccessful { get => _isNativeScanSuccessful; private set => Set(ref _isNativeScanSuccessful, value); }
    public int GraphNodeCount { get => _graphNodeCount; set => Set(ref _graphNodeCount, value); }
    public int GraphEdgeCount { get => _graphEdgeCount; set => Set(ref _graphEdgeCount, value); }
    public string ForgeNarrativeText { get => _forgeNarrativeText; set => Set(ref _forgeNarrativeText, value); }
    public bool IsForgeAttentionRequired { get => _isForgeAttentionRequired; set => Set(ref _isForgeAttentionRequired, value); }
    public string ForgeAttentionMessage { get => _forgeAttentionMessage; set => Set(ref _forgeAttentionMessage, value); }
    public string ForgeTechnicalMessage { get => _forgeTechnicalMessage; set => Set(ref _forgeTechnicalMessage, value); }
    public string ForgePurposeText { get => _forgePurposeText; set => Set(ref _forgePurposeText, value); }
    public int OverallProgressValue { get => _overallProgressValue; set { if (Set(ref _overallProgressValue, value)) Notify(nameof(OverallProgressText)); } }
    public int PhaseProgressValue { get => _phaseProgressValue; set { if (Set(ref _phaseProgressValue, value)) Notify(nameof(PhaseProgressText)); } }
    public bool IsPhaseIndeterminate { get => _isPhaseIndeterminate; set { if (Set(ref _isPhaseIndeterminate, value)) Notify(nameof(PhaseProgressText)); } }
    public string OverallProgressText => $"{OverallProgressValue}%";
    public string PhaseProgressText => IsPhaseIndeterminate ? "Working" : $"{PhaseProgressValue}%";
    public ForgeSessionSnapshot ForgeSession { get => _forgeSession; private set { if (Set(ref _forgeSession, value)) { Notify(nameof(IsForgeSessionVisible)); Notify(nameof(IsForgeRunning)); Notify(nameof(CanLaunchForgedProfile)); Notify(nameof(ForgeElapsedText)); Notify(nameof(CanDeleteSelectedProfile)); } } }
    public CompanionHostProcessSnapshot CompanionHostState { get => _companionHostState; private set { if (Set(ref _companionHostState, value)) Notify(nameof(CompanionHostStatusText)); } }
    public string CompanionHostStatusText => CompanionHostState.State switch
    {
        CompanionHostProcessState.Running => $"Companion Host active · PID {CompanionHostState.ProcessId}",
        CompanionHostProcessState.Starting => "Companion Host starting",
        CompanionHostProcessState.Stopping => "Companion Host stopping",
        CompanionHostProcessState.Faulted => $"Companion Host faulted · {CompanionHostState.Error}",
        _ => _applicationServices.RuntimeSensorHost.IsListening
            ? "Companion IPC ready"
            : "Companion services offline"
    };
    public RuntimeHealth RuntimeHealth { get => _runtimeHealth; private set { if (Set(ref _runtimeHealth, value)) Notify(nameof(RuntimeHealthText)); } }
    public string RuntimeHealthText => $"Health: {RuntimeHealth.Status} · {RuntimeHealth.Component}";
    public BackgroundTaskSnapshot BackgroundTask { get => _backgroundTask; private set { if (Set(ref _backgroundTask, value)) NotifyBackgroundTaskProjection(); } }
    public NotificationSnapshot NotificationState { get => _notificationState; private set { if (Set(ref _notificationState, value)) { Notify(nameof(CurrentNotification)); Notify(nameof(IsControlCenterNotificationVisible)); Notify(nameof(ControlCenterNotificationBrush)); Notify(nameof(ControlCenterNotificationQueueText)); } } }
    public NotificationRequest? CurrentNotification => NotificationState.Current;
    public bool IsControlCenterNotificationVisible => NotificationState.IsVisible;
    public string ControlCenterNotificationQueueText => NotificationState.QueuedCount > 0 ? $"{NotificationState.QueuedCount} queued" : string.Empty;
    public Brush ControlCenterNotificationBrush => CurrentNotification?.Severity switch
    {
        NotificationSeverity.Success => (Brush)FindResource("SuccessBrush"),
        NotificationSeverity.Warning => (Brush)FindResource("WarningBrush"),
        NotificationSeverity.Error => (Brush)FindResource("DangerBrush"),
        _ => (Brush)FindResource("InformationBrush")
    };
    public bool IsBackgroundTaskRunning => BackgroundTask.IsActive;
    public string BackgroundTaskElapsedText => BackgroundTask.State == BackgroundTaskState.Idle ? string.Empty : $"Elapsed {BackgroundTask.Elapsed:hh\\:mm\\:ss}";
    public string BackgroundTaskTechnicalDetail => BackgroundTask.Progress?.TechnicalDetail ?? string.Empty;
    public bool IsForgeSessionVisible => ForgeSession.IsVisible;
    public bool IsForgeRunning => ForgeSession.Status is ForgeSessionStatus.Running or ForgeSessionStatus.Cancelling;
    public bool CanLaunchForgedProfile => ForgeSession.CanLaunchProfile;
    public string ForgeElapsedText => ForgeSession.StartedUtc is null ? string.Empty : $"Elapsed {(ForgeSession.CompletedUtc ?? DateTimeOffset.UtcNow) - ForgeSession.StartedUtc:hh\\:mm\\:ss}";
    public string SelectedProfileStateText => SelectedProfile is null
        ? "No profile selected"
        : SelectedProfile.IsBuiltIn ? "Official DLC only"
        : SelectedProfile.IsLocked ? "Locked"
        : "Editable";

    public string SelectedProfileModCountText
    {
        get
        {
            if (SelectedProfile is null) return "No profile";

            var userModCount = ActiveProfileMods.Count(item =>
                item.Mod?.IsOfficialContent != true);

            return userModCount switch
            {
                0 => "No mods",
                1 => "1 mod",
                _ => $"{userModCount} mods"
            };
        }
    }
    public string SelectedProfileFavoriteGlyph => IsSelectedProfileFavorite ? "★" : "☆";
    public bool IsSelectedProfileFavorite => SelectedProfile is not null && _favoriteProfileNames.Contains(SelectedProfile.Name);
    public bool IsSelectedProfileLocked => SelectedProfile?.IsLocked == true;
    public bool CanRenameSelectedProfile => SelectedProfile is not null && !SelectedProfile.IsBuiltIn && !SelectedProfile.IsLocked;
    public bool CanDeleteSelectedProfile => SelectedProfile is not null && !SelectedProfile.IsBuiltIn && !SelectedProfile.IsLocked && !IsForgeRunning;
    public bool CanLockSelectedProfile => SelectedProfile is not null && !SelectedProfile.IsBuiltIn;
    public bool IsLoadOrderDirty { get => _isLoadOrderDirty; private set => Set(ref _isLoadOrderDirty, value); }
    public bool IsInstantAutoSortEnabled
    {
        get => _isInstantAutoSortEnabled;
        set
        {
            if (!Set(ref _isInstantAutoSortEnabled, value)) return;
            Notify(nameof(AutoSortStateText));
        }
    }

    public string AutoSortStateText => IsInstantAutoSortEnabled ? "Enabled" : "Disabled";
    public ProfileLoadOrderItemViewModel? SelectedLoadOrderItem
    {
        get => _selectedLoadOrderItem;
        set
        {
            if (!Set(ref _selectedLoadOrderItem, value)) return;
            SelectedMod = value?.Mod;
        }
    }

    public int ActiveProfileIssueCount
    {
        get
        {
            if (_analysisSnapshot is null) return 0;
            if (SelectedProfile is null) return _analysisSnapshot.Issues.Count;
            var active = SelectedProfile.ActiveMods.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return _analysisSnapshot.Issues.Count(issue => active.Contains(issue.PackageId));
        }
    }

    public ProfileHealthScore ActiveProfileHealth
    {
        get
        {
            IReadOnlyCollection<string>? active = SelectedProfile?.ActiveMods;
            return _analysisSnapshot?.GetProfileHealth(active) ?? new ProfileHealthScore(100, "Not analyzed", 0, 0, 0, "Run a scan to calculate profile health.");
        }
    }

    public string ActiveProfileHealthLabel
    {
        get
        {
            var health = ActiveProfileHealth;
            if (health.Label.Equals("Not analyzed", StringComparison.OrdinalIgnoreCase)) return "Not Analyzed";

            return health.Score switch
            {
                >= 95 => "Excellent",
                >= 80 => "Healthy",
                >= 60 => "Good",
                >= 40 => "Needs Attention",
                >= 20 => "Poor",
                _ => "Critical"
            };
        }
    }

    public string ActiveProfileHealthText
    {
        get
        {
            var health = ActiveProfileHealth;
            return health.Label.Equals("Not analyzed", StringComparison.OrdinalIgnoreCase)
                ? "Not Analyzed"
                : $"{health.Score} / 100";
        }
    }

    public Brush ActiveProfileHealthBrush
    {
        get
        {
            var health = ActiveProfileHealth;
            if (health.Label.Equals("Not analyzed", StringComparison.OrdinalIgnoreCase))
            {
                return Brushes.Gray;
            }

            return health.Score switch
            {
                >= 80 => new SolidColorBrush(Color.FromRgb(59, 165, 92)),
                >= 60 => new SolidColorBrush(Color.FromRgb(244, 185, 66)),
                >= 40 => new SolidColorBrush(Color.FromRgb(240, 138, 50)),
                >= 20 => new SolidColorBrush(Color.FromRgb(237, 66, 69)),
                _ => new SolidColorBrush(Color.FromRgb(176, 38, 45))
            };
        }
    }

    public bool HasActiveProfileIssues => ActiveProfileIssueCount > 0;

    public string ActiveProfileIssueStatusText => ActiveProfileIssueCount switch
    {
        0 => "Ready",
        1 => "1 issue",
        var count => $"{count} issues"
    };

    public string ActiveProfileIssueSummary => ActiveProfileIssueCount == 0
        ? "No dependency or load-order issues detected for the selected profile."
        : $"{ActiveProfileIssueCount} analysis issue(s) detected in the selected profile.";

    public string ModSorterScopeText
    {
        get
        {
            var health = ActiveProfileHealth;
            if (ShowFullLibrary)
            {
                var errors = _analysisSnapshot?.Issues.Count(issue => issue.Severity == AnalysisIssueSeverity.Error) ?? 0;
                var warnings = _analysisSnapshot?.Issues.Count(issue => issue.Severity == AnalysisIssueSeverity.Warning) ?? 0;
                return $"Full Library — {FormatIssueCounts(errors, warnings)}";
            }

            if (SelectedProfile is null) return "No Active Profile";
            return $"Profile: {SelectedProfile.Name} — {FormatIssueCounts(health.ErrorCount, health.WarningCount)}";
        }
    }

    private static string FormatIssueCounts(int errors, int warnings)
    {
        if (errors == 0 && warnings == 0) return "Healthy";
        var parts = new List<string>(2);
        if (errors > 0) parts.Add(errors == 1 ? "1 Error" : $"{errors} Errors");
        if (warnings > 0) parts.Add(warnings == 1 ? "1 Warning" : $"{warnings} Warnings");
        return string.Join(" • ", parts);
    }


    public bool ShowFullLibrary
    {
        get => _showFullLibrary;
        set
        {
            if (!Set(ref _showFullLibrary, value)) return;
            _workspaceStateService.SetShowFullLibrary(value);
            Notify(nameof(ModSorterScopeText));
            RebuildModSorter();
            RebuildIssueViewer();
            RebuildProfileLoadOrder();
            ModsView.Refresh();
            DependencyEdgesView.Refresh();
        }
    }

    public bool ShowIssuesOnly
    {
        get => _showIssuesOnly;
        set
        {
            if (!Set(ref _showIssuesOnly, value)) return;
            RebuildModSorter();
            RebuildIssueViewer();
            ModsView.Refresh();
        }
    }

    public ModSorterItemViewModel? SelectedSorterItem
    {
        get => _selectedSorterItem;
        set
        {
            if (!Set(ref _selectedSorterItem, value)) return;
            SelectedMod = value?.Mod;
        }
    }


    private IssueWorkItem? _selectedIssueItem;
    public IssueWorkItem? SelectedIssueItem
    {
        get => _selectedIssueItem;
        set
        {
            if (!Set(ref _selectedIssueItem, value)) return;
            if (value is null) return;
            SelectedMod = Mods.FirstOrDefault(mod =>
                string.Equals(mod.PackageId, value.PackageId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public string IssueViewerScopeText => _issueViewerSnapshot?.Summary.ScopeName ?? ModSorterScopeText;
    public string IssueViewerStatusText => _issueViewerSnapshot?.Summary.CanonicalStatus ?? "Not Analyzed";
    public bool HasIssueViewerItems => IssueItems.Count > 0;
    public bool HasAutoFixableIssues => IssueItems.Any(issue => issue.CanAutoFix);

    public string SearchText
    {
        get => _searchContext.QueryText;
        set => _searchContext.SetQuery(value);
    }

    private void SearchContext_QueryChanged(object? sender, string queryText)
    {
        Notify(nameof(SearchText));
        Notify(nameof(SearchValidationText));
        Notify(nameof(HasSearchValidationError));
        RefreshSearchAwareViews();
    }

    public ModRecord? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (!Set(ref _selectedMod, value)) return;
            RecordSelection(value);
            _selectionService.Select(value);
            RecordGlobalNavigationSnapshot();
            NotifyForgeSelectionContext();
            Notify(nameof(SelectedInspectorDisplayName));
            Notify(nameof(SelectedInspectorProfileState));
            Notify(nameof(SelectedInspectorLoadOrderText));
            Notify(nameof(SelectedInspectorSourceIconGeometry));
            Notify(nameof(SelectedInspectorSourceToolTip));
            Notify(nameof(SelectedInspectorShowEvidenceBadges));
            NotifyForgeEvidenceSelectionProperties();
            _ = LoadSelectedSteamContentMetadataAsync(value);
            Notify(nameof(SelectedAnalysisIssueCount));
            Notify(nameof(SelectedAnalysisIsHealthy));
            Notify(nameof(SelectedAnalysisSectionTitle));
            Notify(nameof(SelectedAnalysisStatusText));
            Notify(nameof(SelectedAnalysisSummaryTitle));
            Notify(nameof(SelectedAnalysisVerifiedText));
            Notify(nameof(SelectedAnalysisExplanation));
            Notify(nameof(SelectedDependentPreview));
            Notify(nameof(SelectedRecommendationText));
            NotifyRuntimeEvidenceProperties();
            RefreshDependencyIntelligence();
            var sorterItem = value is null ? null : ModSorterItems.FirstOrDefault(item => item.Mod.Id == value.Id);
            if (!ReferenceEquals(_selectedSorterItem, sorterItem))
            {
                _selectedSorterItem = sorterItem;
                Notify(nameof(SelectedSorterItem));
                Notify(nameof(SelectedInspectorPreviewImagePath));
            }
        }
    }


    public SteamStoreMetadata? SelectedSteamContentMetadata
    {
        get => _selectedSteamContentMetadata;
        private set
        {
            if (!Set(ref _selectedSteamContentMetadata, value)) return;
            Notify(nameof(HasSelectedSteamContentMetadata));
            Notify(nameof(SelectedInspectorPreviewImagePath));
        }
    }

    public bool HasSelectedSteamContentMetadata => SelectedSteamContentMetadata is not null;
    public bool IsSelectedSteamContent => SelectedMod?.IsOfficialContent == true || SelectedMod?.HasWorkshop == true;

    public bool IsSelectedSteamContentLoading
    {
        get => _isSelectedSteamContentLoading;
        private set => Set(ref _isSelectedSteamContentLoading, value);
    }

    public string SelectedSteamContentStatusText
    {
        get => _selectedSteamContentStatusText;
        private set => Set(ref _selectedSteamContentStatusText, value);
    }

    public string SelectedInspectorDisplayName => SelectedMod?.DisplayName ?? "Select a mod";
    public string SelectedInspectorProfileState
    {
        get
        {
            if (SelectedMod is null) return "Select a mod";
            if (SelectedSorterItem is not null) return SelectedSorterItem.ActiveLabel;
            if (SelectedProfile?.ActiveMods.Contains(SelectedMod.PackageId ?? string.Empty, StringComparer.OrdinalIgnoreCase) == true) return "ACTIVE";
            return "LIBRARY";
        }
    }

    public string SelectedInspectorLoadOrderText
    {
        get
        {
            if (SelectedMod is null) return "No load-order position";
            if (SelectedSorterItem?.LoadOrder is int sorterPosition) return (sorterPosition + 1).ToString();
            if (SelectedProfile is null || string.IsNullOrWhiteSpace(SelectedMod.PackageId)) return "No load-order position";
            var index = SelectedProfile.ActiveMods.ToList().FindIndex(id => id.Equals(SelectedMod.PackageId, StringComparison.OrdinalIgnoreCase));
            return index >= 0 ? (index + 1).ToString() : "No load-order position";
        }
    }

    public Geometry SelectedInspectorSourceIconGeometry =>
        ModSourcePresentation.GetIconGeometry(SelectedMod?.Source ?? ModSource.Unknown);

    public string SelectedInspectorSourceToolTip =>
        SelectedMod is null ? "Select a mod to view its source." : ModSourcePresentation.GetToolTip(SelectedMod.Source);

    public bool SelectedInspectorShowEvidenceBadges =>
        SelectedMod is not null && !SelectedMod.IsOfficialContent && SelectedMod.Evidence.Badges.Count > 0;

    public string? SelectedInspectorPreviewImagePath =>
        SelectedSteamContentMetadata?.DisplayImagePath ?? SelectedMod?.PreviewImagePath;

    private async Task LoadSelectedSteamContentMetadataAsync(ModRecord? mod)
    {
        SelectedSteamContentMetadata = null;
        IsSelectedSteamContentLoading = false;
        SelectedSteamContentStatusText = string.Empty;
        Notify(nameof(IsSelectedSteamContent));
        Notify(nameof(SelectedInspectorPreviewImagePath));
        if (mod is null || (!mod.IsOfficialContent && !mod.HasWorkshop)) return;

        IsSelectedSteamContentLoading = true;
        SelectedSteamContentStatusText = mod.IsOfficialContent
            ? "Refreshing Steam store information…"
            : "Loading Steam Workshop information…";

        // Official RimWorld content has a deterministic Steam app mapping. Surface the
        // built-in store card immediately, then replace it with live/cached Steam data.
        if (mod.IsOfficialContent)
        {
            SelectedSteamContentMetadata = _steamStoreMetadataService.GetOfficialFallbackMetadata(mod.PackageId);
        }

        try
        {
            while (_backgroundTaskService.Current.IsActive)
            {
                if (_isClosing || SelectedMod?.Id != mod.Id) return;
                if (IsFeatureTaskRunning("inspector.steam-metadata"))
                    CancelFeatureTask("The selected Steam content changed.");
                await Task.Delay(25);
            }

            if (_isClosing || SelectedMod?.Id != mod.Id) return;
            var metadata = await RunFeatureTaskAsync(
                "inspector.steam-metadata",
                "Steam content refresh",
                async context =>
                {
                    context.Report(new BackgroundTaskProgress(
                        "Loading Steam metadata",
                        mod.IsOfficialContent ? "Refreshing Steam store information" : "Loading Steam Workshop information",
                        mod.IsOfficialContent
                            ? $"Resolving package {mod.PackageId}."
                            : $"Resolving Workshop item {mod.WorkshopId}.",
                        null,
                        0,
                        1,
                        "Checking the local metadata cache and Steam services",
                        mod.Name ?? mod.RootPath));
                    var result = mod.IsOfficialContent
                        ? await _steamStoreMetadataService.GetOfficialMetadataAsync(mod.PackageId, context.CancellationToken)
                        : await _steamStoreMetadataService.GetWorkshopMetadataAsync(mod.WorkshopId, context.CancellationToken);
                    context.Report(new BackgroundTaskProgress(
                        "Steam metadata loaded",
                        "Steam content information is ready",
                        result is null ? "No remote metadata was available." : "Metadata cache is current.",
                        100,
                        1,
                        1,
                        result is null ? "Using available local information" : "Steam content metadata discovered",
                        mod.Name ?? mod.RootPath));
                    return result;
                });
            if (SelectedMod?.Id != mod.Id) return;
            if (metadata is not null)
            {
                SelectedSteamContentMetadata = metadata;
            }
            SelectedSteamContentStatusText = SelectedSteamContentMetadata is null
                ? "Steam information is temporarily unavailable. Local mod information is still shown below."
                : string.Empty;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (SelectedMod?.Id != mod.Id) return;
            SelectedSteamContentStatusText = "Steam information is temporarily unavailable. Local mod information is still shown below.";
            RimForgeLogger.Error("SteamContent", $"Failed to load Steam metadata for {mod.PackageId ?? mod.WorkshopId}.", ex);
        }
        finally
        {
            if (SelectedMod?.Id == mod.Id)
            {
                IsSelectedSteamContentLoading = false;
            }
        }
    }

    private void OpenSelectedSteamContentBrowser_Click(object sender, RoutedEventArgs e)
    {
        var url = SelectedSteamContentMetadata?.StoreUrl ?? SelectedMod?.WorkshopUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            Append("No Steam page is available for the selected mod.");
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void OpenSelectedSteamContentSteam_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSteamContentMetadata is null)
        {
            Append("Steam information is still loading for the selected mod.");
            return;
        }

        var steamUri = SelectedSteamContentMetadata.IsWorkshopItem
            ? $"steam://url/CommunityFilePage/{SelectedSteamContentMetadata.PublishedFileId}"
            : $"steam://store/{SelectedSteamContentMetadata.AppId}";

        try
        {
            Process.Start(new ProcessStartInfo(steamUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Append($"Steam could not be opened ({ex.Message}). Opening the page in the default browser instead.");
            Process.Start(new ProcessStartInfo(SelectedSteamContentMetadata.StoreUrl) { UseShellExecute = true });
        }
    }


    public int SelectedAnalysisIssueCount => SelectedMod is null
        ? 0
        : (_analysisSnapshot?.GetIssues(SelectedMod.PackageId).Count ?? SelectedSorterItem?.AnalysisIssueCount ?? 0);

    public bool SelectedAnalysisIsHealthy => SelectedMod is not null && SelectedAnalysisIssueCount == 0;

    public string SelectedAnalysisSectionTitle => SelectedAnalysisIsHealthy ? "Analysis Results" : "Analysis Issues";

    public string SelectedAnalysisStatusText => SelectedAnalysisIsHealthy
        ? "✓ Analysis complete"
        : $"Found {SelectedAnalysisIssueCount} issue{(SelectedAnalysisIssueCount == 1 ? string.Empty : "s")}";

    public string SelectedAnalysisSummaryTitle => SelectedAnalysisIsHealthy ? "Verified" : "Why";

    public string SelectedAnalysisVerifiedText => SelectedAnalysisIsHealthy
        ? string.Join(Environment.NewLine, "✓ Dependencies", "✓ Metadata", "✓ Compatibility", "✓ Forge Evidence")
        : string.Empty;

    public string SelectedAnalysisExplanation
    {
        get
        {
            if (SelectedMod is null) return "Select a mod to inspect its analysis.";
            var packageId = SelectedMod.PackageId;
            var issues = _analysisSnapshot?.GetIssues(packageId) ?? Array.Empty<ModAnalysisIssue>();
            if (issues.Count == 0) return "This mod passed all current RimForge validation checks." + Environment.NewLine +
                "No dependency, metadata, compatibility, or evidence issues were detected.";
            return string.Join(Environment.NewLine, issues.Take(3).Select(issue => $"• {issue.Explanation}"));
        }
    }

    public string SelectedRecommendationText
    {
        get
        {
            if (SelectedMod is null) return "Select a mod to view recommended actions.";
            var recommendations = _analysisSnapshot?.GetRecommendations(SelectedMod.PackageId) ?? Array.Empty<RepairRecommendation>();
            return recommendations.Count == 0
                ? "No repair actions are recommended for this mod."
                : string.Join(Environment.NewLine, recommendations.Take(3).Select(item => $"• {item.Title}: {item.Explanation}"));
        }
    }

    public string SelectedDependentPreview
    {
        get
        {
            if (SelectedMod is null) return "None";
            var dependents = _analysisSnapshot?.GetTransitiveDependents(SelectedMod.PackageId) ?? Array.Empty<string>();
            if (dependents.Count == 0) return "None";
            var preview = string.Join(", ", dependents.Take(4));
            return dependents.Count > 4 ? $"{preview}, +{dependents.Count - 4} more" : preview;
        }
    }

    public RimForgeProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (ReferenceEquals(_selectedProfile, value)) return;
            if (_isLoadOrderDirty && _selectedProfile is not null && value is not null &&
                !_selectedProfile.Name.Equals(value.Name, StringComparison.OrdinalIgnoreCase))
            {
                var discard = ForgeDialogService.ShowConfirmation(
                    this,
                    "Unsaved Profile Changes",
                    $"'{_selectedProfile.Name}' contains unsaved load-order changes. Discard them and switch to '{value.Name}'?",
                    "Discard and Switch",
                    danger: true);
                if (!discard)
                {
                    Notify(nameof(SelectedProfile));
                    return;
                }
                _isLoadOrderDirty = false;
                Notify(nameof(IsLoadOrderDirty));
            }

            if (!Set(ref _selectedProfile, value)) return;
            _workspaceStateService.SetCurrentProfile(value);
            _undoService.Clear();
            QueueAnalysisRefresh();
            RebuildModSorter();
            RebuildIssueViewer();
            RebuildProfileLoadOrder();
            ModsView.Refresh();
            DependencyEdgesView.Refresh();
            Notify(nameof(ActiveProfileIssueCount));
            Notify(nameof(HasActiveProfileIssues));
            Notify(nameof(ActiveProfileIssueStatusText));
            Notify(nameof(ActiveProfileHealth));
            Notify(nameof(ActiveProfileHealthText));
            Notify(nameof(ActiveProfileHealthLabel));
            Notify(nameof(ActiveProfileHealthBrush));
            Notify(nameof(ActiveProfileIssueSummary));
            Notify(nameof(ModSorterScopeText));
            Notify(nameof(SelectedProfileStateText));
            Notify(nameof(SelectedProfileModCountText));
            Notify(nameof(SelectedProfileFavoriteGlyph));
            Notify(nameof(IsSelectedProfileFavorite));
            Notify(nameof(IsSelectedProfileLocked));
            Notify(nameof(CanRenameSelectedProfile));
            Notify(nameof(CanLockSelectedProfile));
            Notify(nameof(CanDeleteSelectedProfile));
        }
    }

    public MainWindow()
    {
        StartupTimeline.Mark("MainWindow constructor entered", "WPF");
        _smoothScrollTimer.Tick += SmoothScrollTimer_Tick;
        StartupTimeline.Mark("MainWindow InitializeComponent started", "WPF");
        InitializeComponent();
        StartupTimeline.Mark("MainWindow InitializeComponent completed", "WPF");

        StartupTimeline.Mark("Application service composition started", "Composition");
        var services = RimForgeApplicationServices.CreateDefault();
        _applicationServices = services;
        _lifecycleService = services.LifecycleService;
        _diagnosticService = services.DiagnosticService;
        RuntimeHealth = _diagnosticService.CurrentHealth;
        _lifecycleService.Transition(ApplicationLifecycleState.Starting, "Composition", "Application services were composed successfully.");
        StartupTimeline.Mark("Application service composition completed", "Composition");
        _modLibraryService = services.ModLibraryService;
        _analysisEngine = services.AnalysisEngine;
        _forgeEvidenceService = services.ForgeEvidenceService;
        _forgeEvidenceBus = services.ForgeEvidenceBus;
        _forgeEvidenceRefreshScheduler = services.ForgeEvidenceRefreshScheduler;
        _forgeGraphProjectionService = services.ForgeGraphProjectionService;
        _forgeDnaService = services.ForgeDnaService;
        _dependencyIntelligenceService = services.DependencyIntelligenceService;
        _dependencyManagementService = services.DependencyManagementService;
        _nativeForgeRunner = new NativeForgeRunner(_forgeDnaService);
        _profileWorkspaceService = services.ProfileWorkspaceService;
        _profileCatalogStateStore = services.ProfileCatalogStateStore;
        _profilePackageInspectionService = services.ProfilePackageInspectionService;
        _externalProfileReconciliationService = services.ExternalProfileReconciliationService;
        _externalProfileConflictService = services.ExternalProfileConflictService;
        _modsConfigChangeMonitor = services.ModsConfigChangeMonitor;
        _eventBus = services.EventBus;
        _selectionService = services.SelectionService;
        _workspaceStateService = services.WorkspaceStateService;
        _searchContext = services.SearchContext;
        _navigationContext = services.NavigationContext;
        _globalNavigationService = services.GlobalNavigationService;
        _applicationStatusService = services.ApplicationStatusService;
        _undoService = services.UndoService;
        _backgroundTaskService = services.BackgroundTaskService;
        _notificationService = services.NotificationService;
        _commandRegistry = services.CommandRegistry;
        _modFilteringService = services.ModFilteringService;
        _steamLibraryDiscoveryService = services.SteamLibraryDiscoveryService;
        _featureFlagService = services.FeatureFlagService;
        _forgeSessionService = services.ForgeSessionService;
        _companionHost = services.CompanionHost;
        CompanionHostState = _companionHost.Current;
        _gameLogService = services.GameLogService;
        _gameLaunchService = services.GameLaunchService;
        _steamStoreMetadataService = services.SteamStoreMetadataService;
        TextureToolsFeature.ConfigurePaths(services.WorkspaceService.Paths);
        _forgeElapsedTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _forgeElapsedTimer.Tick += ForgeElapsedTimer_Tick;
        _forgeElapsedTimer.Start();
        _eventSubscriptions.Add(_eventBus.Subscribe<ForgeSessionChangedEvent>(applicationEvent =>
            ForgeSessionService_SessionChanged(_eventBus, applicationEvent.Snapshot)));
        _eventSubscriptions.Add(_eventBus.Subscribe<SearchQueryChangedEvent>(applicationEvent =>
            SearchContext_QueryChanged(_eventBus, applicationEvent.QueryText)));
        _eventSubscriptions.Add(_eventBus.Subscribe<NavigationChangedEvent>(_ =>
            NavigationContext_NavigationChanged(_eventBus, EventArgs.Empty)));
        _eventSubscriptions.Add(_eventBus.Subscribe<BackgroundTaskChangedEvent>(applicationEvent =>
            BackgroundTaskService_TaskChanged(_eventBus, applicationEvent.Snapshot)));
        _eventSubscriptions.Add(_eventBus.Subscribe<NotificationChangedEvent>(applicationEvent =>
            NotificationService_NotificationChanged(_eventBus, applicationEvent.Snapshot)));
        _eventSubscriptions.Add(_eventBus.Subscribe<NotificationActionInvokedEvent>(applicationEvent =>
            NotificationService_ActionInvoked(_eventBus, applicationEvent)));
        _undoService.StateChanged += UndoService_StateChanged;
        _globalNavigationService.StateChanged += GlobalNavigationService_StateChanged;
        _applicationStatusService.Changed += ApplicationStatusService_Changed;
        _gameLogService.EntryReceived += GameLogService_EntryReceived;
        _gameLogService.StartupReplayCompleted += GameLogService_StartupReplayCompleted;
        _forgeEvidenceBus.Published += ForgeEvidenceBus_Published;
        _forgeEvidenceService.Invalidated += ForgeEvidenceService_Invalidated;
        _modsConfigChangeMonitor.Changed += ModsConfigChangeMonitor_Changed;
        _companionHost.StateChanged += CompanionHost_StateChanged;
        _diagnosticService.HealthChanged += DiagnosticService_HealthChanged;
        ConfigureCommandFramework();

        ModsView = CollectionViewSource.GetDefaultView(Mods);
        ModsView.Filter = FilterMod;
        ModSorterView = CollectionViewSource.GetDefaultView(ModSorterItems);
        ModSorterView.Filter = FilterSorterItem;
        IssueItemsView = CollectionViewSource.GetDefaultView(IssueItems);
        IssueItemsView.Filter = FilterIssueItem;
        ActiveProfileModsView = CollectionViewSource.GetDefaultView(ActiveProfileMods);
        ActiveProfileModsView.Filter = FilterProfileLoadOrderItem;
        InactiveInstalledModsView = CollectionViewSource.GetDefaultView(InactiveInstalledMods);
        InactiveInstalledModsView.Filter = FilterProfileLoadOrderItem;
        DependencyEdgesView = CollectionViewSource.GetDefaultView(DependencyEdges);
        DependencyEdgesView.Filter = FilterEdge;

        ActiveProfileMods.CollectionChanged += (_, _) =>
        {
            Notify(nameof(SelectedProfileModCountText));
            Notify(nameof(SelectedProfileStateText));
            NotifyDependencyManagementSummary();
            RefreshSearchDiscoveryResults();
            NotifySearchResultState();
        };
        InactiveInstalledMods.CollectionChanged += (_, _) =>
        {
            RefreshSearchDiscoveryResults();
            NotifySearchResultState();
        };
        IssueItems.CollectionChanged += (_, _) =>
        {
            RefreshSearchDiscoveryResults();
            NotifySearchResultState();
        };
        DependencyEdges.CollectionChanged += (_, _) => NotifySearchResultState();

        DataContext = this;
        UpdateNavigationState("Mod Sorter");
        RecordGlobalNavigationSnapshot();
        SourceInitialized += (_, _) => EnableDarkTitleBar();
        Loaded += (_, _) =>
        {
            StartupTimeline.Mark("MainWindow loaded", "WPF");
            // Loaded is intentionally lightweight. The shell is allowed to complete its
            // first layout/render pass before startup services and discovery begin.
            IsNativeScanVisible = true;
            IsNativeScanComplete = false;
            NativeProgressText = "Preparing startup";
        };
        ContentRendered += async (_, _) =>
        {
            StartupTimeline.Mark("First content render", "WPF");
            if (_firstRenderTicks == 0)
                _firstRenderTicks = Stopwatch.GetTimestamp();
            await BeginCoordinatedStartupAsync();
            if (_lifecycleService.Current.State != ApplicationLifecycleState.Failed)
                _lifecycleService.Transition(ApplicationLifecycleState.Running, "Startup", "RimForge startup completed and the shell is interactive.");
        };
        StartupTimeline.Mark("MainWindow constructor completed", "WPF");
        Closing += (_, _) =>
        {
            _isClosing = true;
            CancelFeatureTask("Application shutdown requested.");
            _modsConfigChangeMonitor.Changed -= ModsConfigChangeMonitor_Changed;
            _forgeEvidenceBus.Published -= ForgeEvidenceBus_Published;
            _applicationServices.RuntimeEvidenceStore.SnapshotChanged -= RuntimeEvidenceStore_SnapshotChanged;
            _applicationServices.RuntimeSensorHost.ListeningChanged -= RuntimeSensorHost_ListeningChanged;
            _companionHost.StateChanged -= CompanionHost_StateChanged;
            _diagnosticService.HealthChanged -= DiagnosticService_HealthChanged;
            _ = _gameLogService.StopAsync();
        };
    }

    private void CompanionHost_StateChanged(object? sender, CompanionHostProcessSnapshot snapshot) =>
        Dispatcher.BeginInvoke(new Action(() => CompanionHostState = snapshot));

    private void DiagnosticService_HealthChanged(object? sender, RuntimeHealth health) =>
        Dispatcher.BeginInvoke(new Action(() => RuntimeHealth = health));



    private async Task BeginCoordinatedStartupAsync()
    {
        if (_startupStarted) return;
        _startupStarted = true;
        StartupTimeline.Mark("Startup coordinator entered", "Startup");
        _startupCoordinatorStartedTicks = Stopwatch.GetTimestamp();

        try
        {
            // ContentRendered guarantees the main shell has painted at least once. Yielding at
            // Render priority gives WPF one final opportunity to present the window before any
            // startup stage performs file-system or configuration work.
            StartupTimeline.Mark("Pre-startup render yield started", "WPF");
            await Dispatcher.Yield(DispatcherPriority.Render);
            StartupTimeline.Mark("Pre-startup render yield completed", "WPF");

            var paths = RimForgePathLayout.Create(RepositoryRoot, _outputFolderSetting);
            paths.EnsureGeneratedDirectories();
            var coordinator = new StartupCoordinator(
                Path.Combine(paths.ReportsRoot, "StartupMetrics.json"),
                stage => Dispatcher.BeginInvoke(new Action(() =>
                {
                    NativeProgressText = stage.Name;
                    Append($"{stage.Name}...", ActivitySeverity.Info);
                }), DispatcherPriority.Background),
                stage => Dispatcher.BeginInvoke(new Action(() =>
                {
                    var suffix = stage.Status == "Completed"
                        ? $" complete ({stage.ElapsedMilliseconds:0} ms)"
                        : $" {stage.Status.ToLowerInvariant()}: {stage.Error}";
                    Append($"{stage.Name}{suffix}.", stage.Status == "Completed" ? ActivitySeverity.Success : ActivitySeverity.Error);
                }), DispatcherPriority.Background),
                () => _lastNativeLibraryCacheMetrics,
                () => _lastStartupUiProjectionMetrics);

            var result = await coordinator.RunAsync(
            [
                new StartupStageDefinition(
                    "Validating platform health and recovery state",
                    "Resilience",
                    async cancellationToken =>
                    {
                        var report = await _applicationServices.PlatformValidationService.ValidateAsync(cancellationToken);
                        if (!report.IsHealthy)
                            throw new InvalidOperationException($"Platform self-validation failed {report.FailureCount} check(s).");
                        var recovery = await _applicationServices.ApplicationRecoveryService.BeginRunAsync(
                            File.ReadAllText(Path.Combine(RepositoryRoot, "VERSION")).Trim(),
                            cancellationToken);
                        await _applicationServices.StatePreservationService.CaptureAsync(
                            File.ReadAllText(Path.Combine(RepositoryRoot, "VERSION")).Trim(),
                            cancellationToken);
                        if (!recovery.PreviousShutdownWasClean)
                            Append($"Recovered interrupted RimForge run {recovery.InterruptedRunId}; durable sessions and evidence remain available.", ActivitySeverity.Warning);
                    }),
                new StartupStageDefinition(
                    "Loading feature configuration",
                    "Configuration",
                    cancellationToken => RunFeatureTaskAsync(
                        "startup.feature-configuration",
                        "Load Feature Configuration",
                        context =>
                        {
                            var path = Path.Combine(RepositoryRoot, "Features.json");
                            context.Report(new BackgroundTaskProgress(
                                "Loading feature configuration",
                                "Reading the enabled RimForge feature set.",
                                path,
                                null,
                                0,
                                1,
                                "Validating feature configuration",
                                path));
                            return _featureFlagService.LoadAsync(RepositoryRoot, context.CancellationToken);
                        },
                        cancellationToken)),
                new StartupStageDefinition(
                    "Loading application settings",
                    "Configuration",
                    async _ =>
                    {
                        await LoadSettingsAsync();
                        IsFirstRunGuideVisible = _firstRunGuideRevision < CurrentFirstRunGuideRevision;
                        IsNativeScanVisible = true;
                        IsNativeScanComplete = false;
                    }),
                new StartupStageDefinition(
                    "Resolving RimWorld and Steam paths",
                    "Discovery",
                    _ => EnsureValidModPathsAsync()),
                new StartupStageDefinition(
                    "Building the native mod library",
                    "Discovery",
                    async _ =>
                    {
                        await ScanNativeLibraryAsync();
                        if (!IsNativeScanSuccessful)
                            throw new InvalidOperationException(NativeProgressText);
                    }),
                new StartupStageDefinition(
                    "Starting Runtime Sensor evidence services",
                    "Runtime Evidence",
                    InitializeRuntimeEvidenceAsync)
            ]);

            if (!result.Succeeded)
            {
                var failed = result.Stages.LastOrDefault(stage => stage.Status == "Failed");
                StatusText = "Startup needs attention";
                StatusBrush = (Brush)FindResource("DangerBrush");
                NativeProgressText = failed?.Error ?? "Startup did not complete.";
                IsNativeScanComplete = true;
                Append($"Startup stopped during {failed?.Name ?? "an unknown stage"}: {failed?.Error}", ActivitySeverity.Error);
                _lifecycleService.Transition(
                    ApplicationLifecycleState.Failed,
                    "Startup",
                    failed?.Error ?? "Coordinated startup did not complete.");
            }
        }
        catch (OperationCanceledException ex)
        {
            _lifecycleService.Transition(ApplicationLifecycleState.Failed, "Startup", "Coordinated startup was cancelled.", ex);
            StatusText = "Startup cancelled";
            StatusBrush = Brushes.Gray;
            NativeProgressText = "Startup cancelled";
            IsNativeScanComplete = true;
        }
        catch (Exception ex)
        {
            _lifecycleService.Transition(ApplicationLifecycleState.Failed, "Startup", "Coordinated startup failed.", ex);
            StatusText = "Startup needs attention";
            StatusBrush = (Brush)FindResource("DangerBrush");
            NativeProgressText = ex.Message;
            IsNativeScanComplete = true;
            Append($"Startup coordination failed: {ex.Message}", ActivitySeverity.Error);
        }
    }

    private void ForgeElapsedTimer_Tick(object? sender, EventArgs e)
    {
        if (ForgeSession.StartedUtc is not null && ForgeSession.CompletedUtc is null)
            Notify(nameof(ForgeElapsedText));
        if (BackgroundTask.IsActive)
            BackgroundTask = _backgroundTaskService.Current;
    }

    private void ForgeSessionService_SessionChanged(object? sender, ForgeSessionSnapshot snapshot) => Dispatcher.Invoke(() =>
    {
        _forgeStatusHideCts?.Cancel();
        ForgeSession = snapshot;
        if (LaunchForgedProfileButton is not null)
            LaunchForgedProfileButton.Visibility = snapshot.CanLaunchProfile ? Visibility.Visible : Visibility.Collapsed;
    });

    private void BackgroundTaskService_TaskChanged(object? sender, BackgroundTaskSnapshot snapshot) =>
        Dispatcher.Invoke(() =>
        {
            BackgroundTask = snapshot;
            ProjectBackgroundTaskStatus(snapshot);
            Notify(nameof(CanReforge));
            CommandManager.InvalidateRequerySuggested();
        });

    private void NotificationService_NotificationChanged(object? sender, NotificationSnapshot snapshot) =>
        Dispatcher.Invoke(() => NotificationState = snapshot);


    private void NotificationService_ActionInvoked(object? sender, NotificationActionInvokedEvent applicationEvent)
    {
        if (applicationEvent.ActionId.Equals("undo", StringComparison.OrdinalIgnoreCase) && _undoService.CanUndo)
        {
            Dispatcher.Invoke(() => _undoService.TryUndo());
            return;
        }

        if (applicationEvent.ActionId.Equals("disable-impacted", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.Invoke(() => DisablePendingDependencyRemoval(applicationEvent.NotificationId));
            return;
        }

        if (applicationEvent.ActionId.Equals("remove-orphans", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.Invoke(() => RemovePendingOrphans(applicationEvent.NotificationId));
            return;
        }

        if (applicationEvent.ActionId.Equals("open-profile-export", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.Invoke(() => OpenPendingProfileExport());
            return;
        }

        if (applicationEvent.ActionId.Equals("restore-profile-backup", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.Invoke(async () => await RestorePendingProfileBackupAsync());
            return;
        }

        if (applicationEvent.ActionId.Equals("restore-activation-recovery", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.Invoke(async () => await RestorePendingActivationRecoveryAsync());
            return;
        }

        if (applicationEvent.ActionId.Equals("accept-external-profile", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.Invoke(async () => await AcceptExternalProfileAsync());
            return;
        }

        if (applicationEvent.ActionId.Equals("restore-rimforge-profile", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.Invoke(async () => await RestoreRimForgeProfileAsync());
            return;
        }

        if (applicationEvent.ActionId.Equals("defer-external-profile", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.Invoke(async () => await DeferExternalProfileAsync());
            return;
        }

        if (applicationEvent.ActionId.Equals("view-activity", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.Invoke(() =>
            {
                ScrollToWorkspaceSection(ConsolePanel, "Console");
                ConsoleFeature.SelectActivityTab();
            });
        }
    }

    private void OpenPendingProfileExport()
    {
        if (string.IsNullOrWhiteSpace(_pendingProfileExportPath)) return;
        var directory = Path.GetDirectoryName(_pendingProfileExportPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
    }

    private async Task RestorePendingProfileBackupAsync()
    {
        var backupPath = _pendingProfileBackupPath;
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
        {
            _notificationService.Enqueue(new NotificationRequest(
                "Profile restore unavailable",
                "The recovery backup could not be found.",
                NotificationSeverity.Warning));
            return;
        }

        var defaultName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(backupPath));
        var restoreName = PromptForProfileName("Restore Profile Backup", defaultName + " Restored");
        if (string.IsNullOrWhiteSpace(restoreName)) return;
        var result = await RunProfileOperationAsync(
            "profile.restore",
            "Restore Profile Backup",
            $"Restoring profile '{restoreName}' from its recovery archive.",
            backupPath,
            token => _profileWorkspaceService.RestoreAsync(RepositoryRoot, backupPath, restoreName, token));
        await CompleteProfileOperationAsync(result, selectResultProfile: true);
    }

    private void ControlCenterNotificationAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string actionId })
            _notificationService.InvokeAction(actionId);
    }

    private void DismissControlCenterNotification_Click(object sender, RoutedEventArgs e) =>
        _notificationService.DismissCurrent();

    private void GameLogService_EntryReceived(object? sender, GameLogEntry entry) => Dispatcher.Invoke(() =>
    {
        GameLogEntries.Add(entry);
        if (_gameLogAutoFollow && ConsoleFeature?.GameLogListControl is not null)
            ConsoleFeature.GameLogListControl.ScrollIntoView(entry);
    });

    private void GameLogService_StartupReplayCompleted(object? sender, GameLogReplaySummary summary) => Dispatcher.Invoke(() =>
    {
        var diagnostics = summary.HasDiagnostics
            ? $" ({summary.WarningEntries} warning, {summary.ErrorEntries} error)"
            : string.Empty;
        var finalLine = summary.IncludedUnterminatedFinalLine
            ? " The final in-progress line was preserved."
            : string.Empty;
        Append($"Replayed {summary.ReplayedEntries} Player.log startup entries{diagnostics}.{finalLine}",
            summary.ErrorEntries > 0 ? ActivitySeverity.Warning : ActivitySeverity.Info);
    });

    protected override void OnClosed(EventArgs e)
    {
        _isClosing = true;
        CancelFeatureTask("Application shutdown requested.");

        _forgeElapsedTimer.Stop();
        _forgeElapsedTimer.Tick -= ForgeElapsedTimer_Tick;
        _smoothScrollTimer.Stop();
        _smoothScrollTimer.Tick -= SmoothScrollTimer_Tick;
        _smoothScrollTargets.Clear();

        foreach (var subscription in _eventSubscriptions)
            subscription.Dispose();
        _eventSubscriptions.Clear();
        _gameLogService.EntryReceived -= GameLogService_EntryReceived;
        _gameLogService.StartupReplayCompleted -= GameLogService_StartupReplayCompleted;
        _modsConfigChangeMonitor.Changed -= ModsConfigChangeMonitor_Changed;
            _ = _gameLogService.StopAsync();

        _forgeStatusHideCts?.Cancel();
        _forgeStatusHideCts?.Dispose();

        foreach (var timer in _evidencePopupCloseTimers.Values)
            timer.Stop();
        foreach (var popup in _knownEvidencePopups)
            popup.IsOpen = false;
        _evidencePopupCloseTimers.Clear();
        _pinnedEvidencePopups.Clear();
        _moveEnabledEvidencePopups.Clear();
        _knownEvidencePopups.Clear();
        _activeTransientEvidencePopup = null;
        _draggedEvidencePopup = null;
        _evidenceDragCapture = null;

        _undoService.StateChanged -= UndoService_StateChanged;
        _ = _applicationServices.DisposeAsync().AsTask();

        base.OnClosed(e);
    }

    private string RepositoryRoot => RimForgePathLayout.ResolveRepositoryRoot();

    private RimForgePathLayout RuntimePaths => RimForgePathLayout.Create(RepositoryRoot, OutputFolderSetting);

    private async void RefreshLibrary_Click(object sender, RoutedEventArgs e) => await ScanNativeLibraryAsync();

    private async Task ScanNativeLibraryAsync()
    {
        if (IsFeatureTaskRunning("intelligence.refresh"))
            await StopBackgroundIntelligenceAsync();
        if (_backgroundTaskService.IsRunning) return;

        StatusText = "Loading library";
        StatusBrush = (Brush)FindResource("WarningBrush");
        IsNativeScanVisible = true;
        IsNativeScanComplete = false;
        IsNativeScanSuccessful = false;
        NativeProgressValue = 0;
        NativeProgressText = "Starting native scan";
        ApplyForgeProgress(new ForgeProgress(ForgePhase.Configuration, "Starting native C# mod library scan...", 0, 0));
        Append("Starting native C# mod library scan...", ActivitySeverity.Info);

        // Scanner progress can be extremely chatty for large libraries. Posting every item
        // directly to WPF starves input/rendering, so only the latest update is delivered
        // at a controlled cadence while the scan itself remains fully detailed.
        using var uiProgress = new DispatcherThrottledProgress<ForgeProgress>(
            Dispatcher,
            progress => ApplyForgeProgress(progress),
            TimeSpan.FromMilliseconds(100));

        // On an empty startup library, publish parsed About.xml records immediately so the
        // Dashboard fills while Evidence and dependency analysis continue. Refresh keeps the
        // existing library visible until the replacement snapshot is ready.
        _preliminaryProjectionTicks = 0;
        _preliminaryProjectionCount = 0;
        _lastStartupUiProjectionMetrics = null;
        _acceptPreliminaryDiscovery = Mods.Count == 0;
        if (_acceptPreliminaryDiscovery)
        {
            Mods.Clear();
            ProfileLoadOrderItems.Clear();
            ActiveProfileMods.Clear();
            InactiveInstalledMods.Clear();
        }

        var discoveredModProgress = new Progress<ModRecord>(AddPreliminaryDiscoveredMod);

        try
        {
            var snapshot = await RunFeatureTaskAsync(
                "library.scan",
                "Scan Mod Library",
                context =>
                {
                    var sharedProgress = new SynchronousProgress<ForgeProgress>(update =>
                    {
                        var detailParts = update.TechnicalMessage.Split('\n', 2);
                        var currentFile = detailParts.Length > 1 ? detailParts[1] : string.Empty;
                        context.Report(new BackgroundTaskProgress(
                            update.Phase.ToString(),
                            detailParts[0],
                            currentFile,
                            Math.Clamp(update.OverallProgress * 100d, 0d, 100d),
                            update.Completed,
                            update.Total,
                            $"{update.Phase}: {Math.Max(0, update.Completed)} of {Math.Max(0, update.Total)} processed",
                            currentFile));
                        uiProgress.Report(update);
                    });
                    return _modLibraryService.ScanAsync(
                        RepositoryRoot,
                        sharedProgress,
                        context.CancellationToken,
                        discoveredModProgress,
                        includeEvidence: false);
                });

            _acceptPreliminaryDiscovery = false;
            _lastNativeLibraryCacheMetrics = snapshot.CacheMetrics;
            await ApplyNativeSnapshotAsync(snapshot, CancellationToken.None);
            IsNativeScanSuccessful = true;
            if (snapshot.Mods.Count == 0)
            {
                StatusText = "No mods found";
                StatusBrush = (Brush)FindResource("WarningBrush");
                Append("No RimWorld mods were found. Open Settings to choose folders or use Search Steam Libraries.", ActivitySeverity.Warning);
            }
            else
            {
                StatusText = "Ready";
                StatusBrush = (Brush)FindResource("SuccessBrush");
            }

            var discoveryMessage = $"{snapshot.Mods.Count} mods are ready to use. Background intelligence will continue without blocking RimForge.";
            Append("Discovery complete: " + discoveryMessage, ActivitySeverity.Success);
            _notificationService.Enqueue(new NotificationRequest(
                "Mod library refreshed",
                discoveryMessage,
                NotificationSeverity.Success,
                [new NotificationAction("view-activity", "View Details")],
                TimeSpan.FromSeconds(8)));
            _backgroundIntelligenceTask = StartBackgroundIntelligenceAsync(snapshot.Mods);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
            StatusBrush = Brushes.Gray;
            NativeProgressText = "Native scan cancelled";
            IsNativeScanComplete = true;
            ApplyForgeProgress(new ForgeProgress(ForgePhase.Cancelled, "Native scan cancelled.", OverallProgressValue / 100d, 0));
            Append("Native scan cancelled.", ActivitySeverity.Warning);
            _notificationService.Enqueue(new NotificationRequest(
                "Library refresh cancelled",
                "The existing mod library was left unchanged.",
                NotificationSeverity.Information));
        }
        catch (Exception ex)
        {
            StatusText = "Native scan failed";
            StatusBrush = (Brush)FindResource("DangerBrush");
            NativeProgressText = ex.Message;
            IsNativeScanComplete = true;
            ApplyForgeProgress(new ForgeProgress(ForgePhase.Error, ex.Message, OverallProgressValue / 100d, 0));
            Append("Native scan failed: " + ex.Message, ActivitySeverity.Error);
            _notificationService.Enqueue(new NotificationRequest(
                "Library refresh failed",
                ex.Message,
                NotificationSeverity.Error,
                [new NotificationAction("view-activity", "View Log")],
                TimeSpan.FromSeconds(15)));
        }
        finally
        {
            _acceptPreliminaryDiscovery = false;
        }
    }

    private async Task StartBackgroundIntelligenceAsync(IReadOnlyList<ModRecord> mods)
    {
        var stopwatch = Stopwatch.StartNew();
        var targetVersion = TargetRimWorldVersion;
        var intelligencePaths = RimForgePathLayout.Create(RepositoryRoot, _outputFolderSetting);
        var intelligenceReportPath = Path.Combine(intelligencePaths.ReportsRoot, "IntelligenceMetrics.json");

        try
        {
            Append("Shared evidence refresh started. Cached generations will be reused where possible.", ActivitySeverity.Info);
            _forgeEvidenceService.StartWatching(mods);
            _forgeEvidenceRefreshScheduler.Configure(new ForgeEvidenceRefreshRequest(
                mods,
                RepositoryRoot,
                targetVersion));
            _forgeEvidenceRefreshScheduler.Start();
            var snapshot = await RunFeatureTaskAsync(
                "intelligence.refresh",
                "Refresh Shared Mod Intelligence",
                async context =>
                {
                    var progress = new SynchronousProgress<ForgeEvidenceProgress>(update =>
                    {
                        var percent = update.Total <= 0 ? 0d : update.Completed * 100d / update.Total;
                        context.Report(new BackgroundTaskProgress(
                            "Refreshing shared evidence",
                            $"{update.ModName} ({update.Completed}/{update.Total})",
                            update.RootPath,
                            percent,
                            update.Completed,
                            update.Total,
                            update.CacheHit ? "Reused cached Forge evidence" : "Scanned changed Forge evidence",
                            update.RootPath));
                        if (update.Total > 0 && (update.Completed == update.Total || update.Completed % 25 == 0))
                        {
                            Append(
                                $"Shared evidence {(update.CacheHit ? "reused" : "scanned")}: {update.ModName} ({update.Completed}/{update.Total}) — {update.RootPath}",
                                ActivitySeverity.Info);
                        }
                    });
                    var refreshed = await _forgeEvidenceService.RefreshAsync(
                        mods,
                        RepositoryRoot,
                        targetVersion,
                        progress,
                        context.CancellationToken);
                    intelligencePaths.EnsureGeneratedDirectories();
                    context.Report(new BackgroundTaskProgress(
                        "Writing intelligence report",
                        "Persisting shared intelligence metrics.",
                        intelligenceReportPath,
                        99d,
                        refreshed.Metrics.Requested,
                        Math.Max(1, refreshed.Metrics.Requested),
                        "Shared evidence generation is ready",
                        intelligenceReportPath));
                    await File.WriteAllTextAsync(
                        intelligenceReportPath,
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            Generated = DateTimeOffset.Now,
                            SharedEvidenceGeneration = refreshed.Generation,
                            refreshed.Metrics.Requested,
                            refreshed.Metrics.Scanned,
                            refreshed.Metrics.Reused,
                            refreshed.Metrics.CacheMisses,
                            refreshed.Metrics.CorruptRecovered,
                            refreshed.Metrics.CoalescedRequests,
                            refreshed.Metrics.DebouncedInvalidations,
                            refreshed.Metrics.WatcherOverflows,
                            refreshed.Metrics.PendingInvalidations,
                            refreshed.Metrics.ReconciledContributions,
                            refreshed.Metrics.ActiveWatchers,
                            refreshed.Metrics.CacheFilesDeleted,
                            refreshed.Metrics.TemporaryFilesDeleted,
                            refreshed.Metrics.QuarantineFilesDeleted,
                            refreshed.Metrics.Failed,
                            refreshed.Metrics.Removed,
                            ElapsedMilliseconds = refreshed.Metrics.Elapsed.TotalMilliseconds,
                            BlockingStartup = false
                        }, RimForgeJson.Indented),
                        context.CancellationToken);
                    return refreshed;
                });
            stopwatch.Stop();

            await Dispatcher.InvokeAsync(() =>
            {
                SharedEvidenceGeneration = snapshot.Generation;
                ApplyForgeEvidenceSnapshot(snapshot);
                QueueAnalysisRefresh();
                RebuildModSorter();
                RebuildProfileLoadOrder();
                Append(
                    $"Shared evidence generation {snapshot.Generation} published: " +
                    $"{snapshot.Metrics.Scanned} scanned, {snapshot.Metrics.Reused} reused, " +
                    $"{snapshot.Metrics.CacheMisses} cache misses, {snapshot.Metrics.CorruptRecovered} corrupt cache entries recovered, " +
                    $"{snapshot.Metrics.CoalescedRequests} requests coalesced, {snapshot.Metrics.DebouncedInvalidations} watcher bursts debounced, " +
                    $"{snapshot.Metrics.WatcherOverflows} watcher overflows, {snapshot.Metrics.CacheFilesDeleted} stale cache files deleted, " +
                    $"{snapshot.Metrics.TemporaryFilesDeleted} temporary files deleted, {snapshot.Metrics.QuarantineFilesDeleted} quarantine files deleted, " +
                    $"{snapshot.Metrics.Failed} failed in {snapshot.Metrics.Elapsed.TotalSeconds:0.0} seconds.",
                    snapshot.Metrics.Failed == 0 ? ActivitySeverity.Success : ActivitySeverity.Warning);
            }, DispatcherPriority.Background);

        }
        catch (OperationCanceledException)
        {
            // Closing or refreshing RimForge may cancel optional intelligence safely.
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await Dispatcher.InvokeAsync(() =>
                Append($"Shared evidence refresh paused: {ex.Message}", ActivitySeverity.Warning));
        }
        finally
        {
            _backgroundIntelligenceTask = null;
        }
    }

    private async Task StopBackgroundIntelligenceAsync()
    {
        var task = _backgroundIntelligenceTask;
        if (task is null) return;
        if (!task.IsCompleted)
            CancelFeatureTask("Stopping shared intelligence before the next feature operation.");
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // The shared lifecycle already published the cancellation state.
        }
        _backgroundIntelligenceTask = null;
    }

    private void ApplyBackgroundIntelligenceUpdate(ModRecord mod)
    {
        // Evidence enrichment mutates the existing ModRecord. Notify only the rows that
        // reference that record; do not rebuild or refresh the complete library views.
        foreach (var item in ModSorterItems)
        {
            if (ReferenceEquals(item.Mod, mod) || item.Mod.Id.Equals(mod.Id, StringComparison.OrdinalIgnoreCase))
                item.NotifyEvidenceChanged();
        }

        var notified = new HashSet<ProfileLoadOrderItemViewModel>();
        foreach (var item in ActiveProfileMods.Concat(InactiveInstalledMods))
        {
            if (!notified.Add(item)) continue;
            if (ReferenceEquals(item.Mod, mod) || item.Mod?.Id.Equals(mod.Id, StringComparison.OrdinalIgnoreCase) == true)
                item.NotifyEvidenceChanged();
        }

        if (SelectedMod?.Id.Equals(mod.Id, StringComparison.OrdinalIgnoreCase) == true)
        {
            Notify(nameof(SelectedMod));
            Notify(nameof(SelectedInspectorShowEvidenceBadges));
            NotifyForgeEvidenceSelectionProperties();
        }
    }

    private void AddPreliminaryDiscoveredMod(ModRecord mod)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (!_acceptPreliminaryDiscovery) return;
        if (Mods.Any(existing => existing.Id.Equals(mod.Id, StringComparison.OrdinalIgnoreCase))) return;

        Mods.Add(mod);

        if (string.IsNullOrWhiteSpace(mod.PackageId)) return;

        var isActive = SelectedProfile?.ActiveMods.Contains(
            mod.PackageId,
            StringComparer.OrdinalIgnoreCase) == true;
        var position = 0;
        if (isActive && SelectedProfile is not null)
        {
            for (var index = 0; index < SelectedProfile.ActiveMods.Count; index++)
            {
                if (!SelectedProfile.ActiveMods[index].Equals(mod.PackageId, StringComparison.OrdinalIgnoreCase)) continue;
                position = index + 1;
                break;
            }
        }
        var item = new ProfileLoadOrderItemViewModel(
            position,
            mod.PackageId,
            mod,
            isActive,
            analysisHealthLabel: "Pending");

        ProfileLoadOrderItems.Add(item);
        if (isActive)
        {
            ActiveProfileMods.Add(item);
        }
        else
        {
            InactiveInstalledMods.Add(item);
        }
        }
        finally
        {
            Interlocked.Increment(ref _preliminaryProjectionCount);
            Interlocked.Add(ref _preliminaryProjectionTicks, Stopwatch.GetTimestamp() - started);
        }
    }

    private async Task ApplyNativeSnapshotAsync(ModLibrarySnapshot snapshot, CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        cancellationToken.ThrowIfCancellationRequested();

        // Build analysis and sorter projections away from the UI thread. Only the final
        // observable-collection updates are marshalled back through this method.
        var activeLoadOrder = SelectedProfile?.ActiveMods.ToArray();
        var selectedProfileWorkspace = SelectedProfile?.WorkspacePath ?? RepositoryRoot;
        var selectedProfileConfig = SelectedProfile?.ModsConfigPath ?? string.Empty;
        var analysisElapsed = TimeSpan.Zero;
        var sorterBuildElapsed = TimeSpan.Zero;
        var projection = await RunFeatureTaskAsync(
            "library.projection",
            "Build Library Projection",
            async context =>
            {
                context.Report(new BackgroundTaskProgress(
                    "Analyzing discovered mods",
                    $"Building shared analysis for {snapshot.Mods.Count} mod(s).",
                    RepositoryRoot,
                    10d,
                    0,
                    Math.Max(1, snapshot.Mods.Count),
                    "Resolving dependency, issue, and Forge DNA state",
                    RepositoryRoot));
                var analysisStopwatch = Stopwatch.StartNew();
                var forgeDnaSnapshot = await _forgeDnaService.AnalyzeAsync(
                    snapshot.Mods.ToList(),
                    activeLoadOrder,
                    TargetRimWorldVersion,
                    _forgeEvidenceSnapshot.Contributions,
                    cancellationToken: context.CancellationToken).ConfigureAwait(false);
                analysisStopwatch.Stop();
                analysisElapsed = analysisStopwatch.Elapsed;

                context.CancellationToken.ThrowIfCancellationRequested();
                context.Report(new BackgroundTaskProgress(
                    "Building Mod Sorter projection",
                    "Projecting active profile state and findings.",
                    selectedProfileWorkspace,
                    70d,
                    snapshot.Mods.Count,
                    Math.Max(1, snapshot.Mods.Count),
                    "Preparing the unified library workspace",
                    selectedProfileConfig));
                var sorterBuildStopwatch = Stopwatch.StartNew();
                var sorterItems = BuildModSorterItems(snapshot.Mods, activeLoadOrder, forgeDnaSnapshot.Analysis);
                sorterBuildStopwatch.Stop();
                sorterBuildElapsed = sorterBuildStopwatch.Elapsed;
                context.Report(new BackgroundTaskProgress(
                    "Synchronizing library UI",
                    $"Prepared {sorterItems.Count} Mod Sorter row(s).",
                    RepositoryRoot,
                    100d,
                    snapshot.Mods.Count,
                    Math.Max(1, snapshot.Mods.Count),
                    "Projection ready for UI synchronization",
                    selectedProfileConfig));
                return (Analysis: forgeDnaSnapshot.Analysis, SorterItems: sorterItems);
            });
        var analysisSnapshot = projection.Analysis;
        var sorterItems = projection.SorterItems;

        cancellationToken.ThrowIfCancellationRequested();

        var modsCollectionStopwatch = Stopwatch.StartNew();
        await ReplaceCollectionInResponsiveBatchesAsync(
            Mods,
            ModsView,
            snapshot.Mods,
            cancellationToken);
        modsCollectionStopwatch.Stop();

        var orderedNodes = snapshot.DependencyGraph.Nodes
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.PackageId ?? node.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        await ReplaceCollectionInResponsiveBatchesAsync(
            DependencyNodes,
            CollectionViewSource.GetDefaultView(DependencyNodes),
            orderedNodes,
            cancellationToken);

        var orderedEdges = snapshot.DependencyGraph.Edges
            .OrderBy(edge => edge.SourceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.TargetId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var edgesCollectionStopwatch = Stopwatch.StartNew();
        await ReplaceCollectionInResponsiveBatchesAsync(
            DependencyEdges,
            DependencyEdgesView,
            orderedEdges,
            cancellationToken);
        edgesCollectionStopwatch.Stop();

        GraphNodeCount = snapshot.DependencyGraph.Nodes.Count;
        GraphEdgeCount = snapshot.DependencyGraph.Edges.Count;
        Summary = new AuditSummary(
            snapshot.Mods.Count,
            snapshot.MissingDependencies.Count,
            snapshot.Cycles.Count,
            snapshot.Validation.MissingNames + snapshot.Validation.MissingPackageIds,
            snapshot.Generated);

        _analysisSnapshot = analysisSnapshot;
        NotifyAnalysisProperties();
        RebuildIssueViewer();

        var previousId = SelectedMod?.Id;
        var sorterCollectionStopwatch = Stopwatch.StartNew();
        await ReplaceCollectionInResponsiveBatchesAsync(
            ModSorterItems,
            ModSorterView,
            sorterItems,
            cancellationToken);
        sorterCollectionStopwatch.Stop();

        SelectedSorterItem = previousId is null
            ? ModSorterItems.FirstOrDefault()
            : ModSorterItems.FirstOrDefault(item => item.Mod.Id == previousId) ?? ModSorterItems.FirstOrDefault();
        SelectedMod = SelectedSorterItem?.Mod ?? Mods.FirstOrDefault();

        NativeProgressValue = 100;
        NativeProgressText = $"Loaded {snapshot.Mods.Count} mods natively";
        IsNativeScanComplete = true;
        if (!IsFirstRunGuideVisible)
        {
            await Task.Delay(900);
            IsNativeScanVisible = false;
        }
        ModSorterFeature.EmptyState.Visibility = snapshot.Mods.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Give WPF one render opportunity before profile rebuilding performs its own
        // bound-list updates. This prevents a completed refresh from appearing frozen.
        await Dispatcher.Yield(DispatcherPriority.Background);
        var profileLoadStopwatch = Stopwatch.StartNew();
        await LoadProfilesAsync();
        // Profile loading reports its own recoverable failures. Always publish a list
        // projection afterward so successful discovery cannot leave the workspace blank.
        RebuildProfileLoadOrder();
        profileLoadStopwatch.Stop();
        totalStopwatch.Stop();

        var nowTicks = Stopwatch.GetTimestamp();
        _lastStartupUiProjectionMetrics = new StartupUiProjectionMetrics(
            _preliminaryProjectionCount,
            (_preliminaryProjectionTicks * 1000d) / Stopwatch.Frequency,
            analysisElapsed.TotalMilliseconds,
            sorterBuildElapsed.TotalMilliseconds,
            modsCollectionStopwatch.Elapsed.TotalMilliseconds,
            edgesCollectionStopwatch.Elapsed.TotalMilliseconds,
            sorterCollectionStopwatch.Elapsed.TotalMilliseconds,
            profileLoadStopwatch.Elapsed.TotalMilliseconds,
            totalStopwatch.Elapsed.TotalMilliseconds)
        {
            TimeToUsableMilliseconds = _startupCoordinatorStartedTicks == 0
                ? 0
                : ((nowTicks - _startupCoordinatorStartedTicks) * 1000d) / Stopwatch.Frequency,
            FirstRenderToUsableMilliseconds = _firstRenderTicks == 0
                ? 0
                : ((nowTicks - _firstRenderTicks) * 1000d) / Stopwatch.Frequency
        };

        StartupTimeline.Mark(
            "Usable UI ready",
            "Startup",
            $"{snapshot.Mods.Count} mods available; profile projection completed");
        var startupPaths = RimForgePathLayout.Create(RepositoryRoot, _outputFolderSetting);
        startupPaths.EnsureGeneratedDirectories();
        await StartupTimeline.WriteAsync(
            Path.Combine(startupPaths.ReportsRoot, "StartupTimeline.json"),
            cancellationToken);
    }

    private IReadOnlyList<ModSorterItemViewModel> BuildModSorterItems(
        IReadOnlyList<ModRecord> mods,
        IReadOnlyList<string>? activeLoadOrder,
        ModAnalysisSnapshot? analysisSnapshot)
    {
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (activeLoadOrder is not null)
        {
            for (var index = 0; index < activeLoadOrder.Count; index++)
            {
                var packageId = activeLoadOrder[index];
                if (!string.IsNullOrWhiteSpace(packageId)) order.TryAdd(packageId, index);
            }
        }

        var activeIds = order.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sortDecisions = analysisSnapshot?.ProposedOrder.Decisions
            .GroupBy(decision => decision.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, LoadOrderDecision>(StringComparer.OrdinalIgnoreCase);
        var scopedMods = mods.Where(mod =>
        {
            var packageId = mod.PackageId ?? mod.Id;
            var isActive = activeIds.Contains(packageId);
            var issueCount = analysisSnapshot?.GetSummary(packageId).IssueCount ?? mod.Errors.Count;
            if (!ShowFullLibrary && !isActive) return false;
            if (ShowIssuesOnly && analysisSnapshot is not null && issueCount == 0) return false;
            return true;
        });

        return scopedMods
            .Select(mod =>
            {
                var index = -1;
                var active = !string.IsNullOrWhiteSpace(mod.PackageId) && order.TryGetValue(mod.PackageId, out index);
                var packageId = mod.PackageId ?? mod.Id;
                var analysis = analysisSnapshot?.GetSummary(packageId);
                sortDecisions.TryGetValue(packageId, out var sortDecision);
                return new ModSorterItemViewModel(
                    mod,
                    active,
                    active ? index : null,
                    analysis?.IssueCount ?? mod.Errors.Count,
                    analysis?.DirectDependentCount ?? 0,
                    analysis?.TransitiveDependentCount ?? 0,
                    analysis?.IsInCycle ?? false,
                    analysis?.HealthLabel,
                    analysis?.ImpactLabel,
                    sortDecision?.ProposedIndex,
                    sortDecision?.PrimaryReason,
                    sortDecision?.RuleSource,
                    sortDecision?.Confidence ?? LoadOrderRuleConfidence.Experimental,
                    sortDecision?.IsRequired ?? false);
            })
            .OrderBy(item => item.IsActive ? 0 : 1)
            .ThenBy(item => item.LoadOrder ?? int.MaxValue)
            .ThenBy(item => item.Mod.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Task ReplaceCollectionInResponsiveBatchesAsync<T>(
        BulkObservableCollection<T> target,
        ICollectionView view,
        IEnumerable<T> items,
        CancellationToken cancellationToken,
        int batchSize = 64)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var materialized = items as IReadOnlyList<T> ?? items.ToArray();

        // Atomic replacement prevents hundreds of CollectionChanged notifications and
        // repeated WPF sorting/filtering/layout work. One Reset notification is emitted,
        // followed by one explicit view refresh.
        target.ReplaceAll(materialized);
        return Task.CompletedTask;
    }

    private void NotifyAnalysisProperties()
    {
        Notify(nameof(SelectedAnalysisIssueCount));
        Notify(nameof(SelectedAnalysisIsHealthy));
        Notify(nameof(SelectedAnalysisSectionTitle));
        Notify(nameof(SelectedAnalysisStatusText));
        Notify(nameof(SelectedAnalysisSummaryTitle));
        Notify(nameof(SelectedAnalysisVerifiedText));
        Notify(nameof(SelectedAnalysisExplanation));
        Notify(nameof(SelectedDependentPreview));
        Notify(nameof(SelectedRecommendationText));
        Notify(nameof(ActiveProfileHealth));
        Notify(nameof(ActiveProfileIssueCount));
        Notify(nameof(HasActiveProfileIssues));
        Notify(nameof(ActiveProfileIssueStatusText));
        Notify(nameof(ActiveProfileHealthText));
        Notify(nameof(ActiveProfileHealthLabel));
        Notify(nameof(ActiveProfileHealthBrush));
        Notify(nameof(ActiveProfileIssueSummary));
        Notify(nameof(ModSorterScopeText));
    }

    private async void RunAudit_Click(object sender, RoutedEventArgs e)
    {
        if (_nativeForgeRunner.IsRunning || IsForgeRunning)
        {
            Cancel_Click(sender, e);
            return;
        }

        if (IsFeatureTaskRunning("intelligence.refresh"))
            await StopBackgroundIntelligenceAsync();
        if (_backgroundTaskService.IsRunning) return;

        IsForgeAttentionRequired = false;
        ForgeAttentionMessage = string.Empty;
        _forgedProfile = SelectedProfile;
        var paths = RimForgePathLayout.Create(RepositoryRoot, _outputFolderSetting);
        paths.EnsureGeneratedDirectories();
        _forgeSessionService.Start(new ForgeSessionRequest(
            SelectedProfile?.WorkspacePath ?? paths.ProfilesRoot,
            SelectedProfile?.Name ?? "Current profile",
            TargetRimWorldVersion,
            Mods.Count,
            "Starting native .NET Forge analysis..."));
        StatusText = "Forging";
        StatusBrush = (Brush)FindResource("WarningBrush");
        ApplyForgeProgress(new ForgeProgress(ForgePhase.Configuration, "Starting native .NET Forge analysis...", 0, 0));
        Append("The native .NET Forge has been ignited.", ActivitySeverity.Info);

        try
        {
            var forgeMods = Mods.ToList();
            var forgeProfile = SelectedProfile;
            var forgeStopwatch = Stopwatch.StartNew();
            var evidenceStageStopwatch = Stopwatch.StartNew();
            var evidenceSnapshot = await RunFeatureTaskAsync(
                "forge.evidence",
                "Authoritative Forge evidence generation",
                context =>
                {
                    context.Report(new BackgroundTaskProgress(
                        "Preparing evidence cache",
                        "Validating cached Forge evidence before scanning changed mods.",
                        RepositoryRoot,
                        0d,
                        0,
                        forgeMods.Count,
                        "Validating incremental evidence cache",
                        RepositoryRoot));
                    Dispatcher.Invoke(() => ApplyForgeProgress(
                        new ForgeProgress(
                            ForgePhase.Configuration,
                            $"Preparing incremental Forge evidence for {forgeMods.Count} installed mod(s).\n{RepositoryRoot}",
                            0.01,
                            0d,
                            0,
                            Math.Max(1, forgeMods.Count)),
                        reportBackgroundTask: false));

                    var evidenceProgress = new SynchronousProgress<ForgeEvidenceProgress>(update =>
                    {
                        var fraction = update.Total <= 0 ? 0d : update.Completed / (double)update.Total;
                        context.Report(new BackgroundTaskProgress(
                            "Scanning Forge evidence",
                            $"{update.ModName} ({update.Completed}/{update.Total})",
                            update.RootPath,
                            Math.Clamp(fraction * 68d, 0d, 68d),
                            update.Completed,
                            update.Total,
                            update.ModName,
                            update.RootPath));
                        Dispatcher.Invoke(() => ApplyForgeProgress(
                            new ForgeProgress(
                                ForgePhase.EvidenceScan,
                                $"Deep-scanning evidence: {update.ModName} ({update.Completed}/{update.Total})\n{update.RootPath}",
                                Math.Clamp(fraction * 0.68, 0d, 0.68),
                                fraction,
                                Math.Max(1, update.Completed),
                                Math.Max(1, update.Total)),
                            reportBackgroundTask: false));
                    });

                    return _forgeEvidenceService.RefreshAsync(
                        forgeMods,
                        RepositoryRoot,
                        TargetRimWorldVersion,
                        evidenceProgress,
                        context.CancellationToken,
                        forceRescan: true);
                });

            evidenceStageStopwatch.Stop();
            ApplyForgeEvidenceSnapshot(evidenceSnapshot);
            // Publish the authoritative library projection before profile-scoped analysis.
            // If the later analysis stage fails, every discovered mod still remains visible
            // in Mod Sorter, active/inactive lists, Inspector, search, and Dependency Map.
            RebuildModSorter();
            RebuildProfileLoadOrder();
            Append(
                $"Pass 48 Forge evidence stage completed in {evidenceStageStopwatch.Elapsed.TotalSeconds:0.00} seconds: generation {evidenceSnapshot.Generation}, {evidenceSnapshot.Metrics.Scanned} scanned, {evidenceSnapshot.Metrics.Reused} reused, {evidenceSnapshot.Metrics.Failed} failed.",
                evidenceSnapshot.Metrics.Failed == 0 ? ActivitySeverity.Success : ActivitySeverity.Warning);

            var analysisStageStopwatch = Stopwatch.StartNew();
            var result = await RunFeatureTaskAsync(
                "forge.analysis",
                "Forge Analysis",
                context =>
                {
                    context.Report(new BackgroundTaskProgress(
                        "Preparing native analysis",
                        "Building the profile-scoped dependency and issue model.",
                        paths.ReportsRoot,
                        68d,
                        0,
                        9,
                        "Preparing the active profile analysis pipeline",
                        forgeProfile?.ModsConfigPath ?? paths.ReportsRoot));
                    var progress = new SynchronousProgress<ForgeProgress>(update =>
                    {
                        context.Report(new BackgroundTaskProgress(
                            update.Phase.ToString(),
                            update.TechnicalMessage.Split('\n')[0],
                            update.TechnicalMessage.Contains('\n') ? update.TechnicalMessage[(update.TechnicalMessage.IndexOf('\n') + 1)..] : string.Empty,
                            Math.Clamp(update.OverallProgress * 100d, 0d, 100d),
                            update.Completed,
                            update.Total,
                            update.Phase.ToString(),
                            update.TechnicalMessage.Contains('\n') ? update.TechnicalMessage[(update.TechnicalMessage.IndexOf('\n') + 1)..] : string.Empty));
                        Dispatcher.Invoke(() => ApplyForgeProgress(update, reportBackgroundTask: false));
                    });
                    return _nativeForgeRunner.RunAsync(
                        forgeMods,
                        forgeProfile,
                        TargetRimWorldVersion,
                        _forgeEvidenceSnapshot.Contributions,
                        paths.ReportsRoot,
                        progress,
                        context.CancellationToken);
                });
            analysisStageStopwatch.Stop();
            forgeStopwatch.Stop();
            Append($"Pass 48 native analysis stage completed in {analysisStageStopwatch.Elapsed.TotalSeconds:0.00} seconds; total Forge pipeline {forgeStopwatch.Elapsed.TotalSeconds:0.00} seconds.", ActivitySeverity.Success);

            _analysisSnapshot = result.Snapshot;
            Summary = result.Summary;
            NotifyAnalysisProperties();
            RebuildIssueViewer();
            RebuildModSorter();
            // Keep both list projections synchronized with the Forge snapshot. If profile
            // publication failed, the recovery projection still exposes every installed mod.
            RebuildProfileLoadOrder();

            StatusText = "Ready";
            StatusBrush = (Brush)FindResource("SuccessBrush");
            var completionMessage = BuildForgeCompletionMessage();
            _forgeSessionService.Complete(completionMessage);
            var forgeDurationMessage = $"Completed in {result.Elapsed.TotalSeconds:0.00} seconds. {completionMessage}";
            Append($"Native .NET Forge completed in {result.Elapsed.TotalSeconds:0.00} seconds.", ActivitySeverity.Success);
            _notificationService.Enqueue(new NotificationRequest(
                "Forge complete",
                forgeDurationMessage,
                ActiveProfileIssueCount == 0 ? NotificationSeverity.Success : NotificationSeverity.Warning,
                [new NotificationAction("view-activity", "View Details")],
                TimeSpan.FromSeconds(10)));
            foreach (var report in result.WrittenReports)
                Append($"Wrote report: {report}", ActivitySeverity.Info);

            if (ActiveProfileIssueCount == 0)
            {
                IsForgeAttentionRequired = false;
                ForgeAttentionMessage = string.Empty;
                ScheduleForgeStatusAutoHide();
            }
            else
            {
                IsForgeAttentionRequired = true;
                ForgeNarrativeText = "The forge needs attention.";
                ForgeAttentionMessage = $"{ActiveProfileIssueCount} active finding(s) remain. Resolve the current issue or ignore it for now to continue reviewing the queue.";
            }
        }
        catch (OperationCanceledException)
        {
            ApplyForgeProgress(new ForgeProgress(ForgePhase.Cancelled, "Native Forge cancelled.", OverallProgressValue / 100d, 0));
            _forgeSessionService.Cancel();
            Append("Native Forge cancelled.", ActivitySeverity.Warning);
            _notificationService.Enqueue(new NotificationRequest(
                "Forge cancelled",
                "No Forge results were applied.",
                NotificationSeverity.Information));
            StatusText = "Cancelled";
            StatusBrush = Brushes.Gray;
        }
        catch (Exception ex)
        {
            // Preserve the last authoritative discovery/evidence projection even when a
            // downstream Forge stage fails. A failed analysis must not blank the workspace.
            RebuildModSorter();
            RebuildProfileLoadOrder();
            ApplyForgeProgress(new ForgeProgress(ForgePhase.Error, ex.Message, OverallProgressValue / 100d, 0));
            _forgeSessionService.Fail(ex.Message, ex);
            Append("Native Forge failed: " + ex.Message, ActivitySeverity.Error);
            StatusText = "Forge failed";
            StatusBrush = (Brush)FindResource("DangerBrush");
            IsForgeAttentionRequired = true;
            ForgeAttentionMessage = "The Forge stopped on a technical failure. Resolve opens the most relevant issue when available, otherwise the Console; Ignore for Now dismisses this checkpoint so the rest of RimForge remains usable.";
        }
    }


    private IssueWorkItem? GetNextForgeAttentionIssue()
    {
        return IssueItems
            .Where(issue => !issue.IsIgnored)
            .OrderByDescending(issue => issue.Severity == AnalysisIssueSeverity.Error)
            .ThenByDescending(issue => issue.Severity == AnalysisIssueSeverity.Warning)
            .ThenBy(issue => issue.ModName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private void ResolveForgeAttention_Click(object sender, RoutedEventArgs e)
    {
        var issue = GetNextForgeAttentionIssue();
        if (issue is not null)
        {
            SelectedIssueItem = issue;
            ScrollToWorkspaceSection(IssueViewerWorkspacePanel, "Issue Viewer");
            IssueViewerFeature.FocusIssue(issue);
            return;
        }

        ScrollToWorkspaceSection(ConsolePanel, "Console");
        ConsoleFeature.SelectActivityTab();
    }

    private void IgnoreForgeAttention_Click(object sender, RoutedEventArgs e)
    {
        var issue = GetNextForgeAttentionIssue();
        if (issue is not null)
        {
            IssueIgnoreStore.SetIgnored(issue.Id, true);
            RebuildIssueViewer();
            NotifyAnalysisProperties();
            RebuildModSorter();
            RebuildProfileLoadOrder();
            Append($"Ignored Forge finding for now: {issue.ModName} — {issue.Title}", ActivitySeverity.Warning);
        }

        var next = GetNextForgeAttentionIssue();
        if (next is not null)
        {
            SelectedIssueItem = next;
            ForgeAttentionMessage = $"Ignored for now. {IssueItems.Count(item => !item.IsIgnored)} active finding(s) remain; the next finding is ready for review.";
            ForgeNarrativeText = "The forge needs attention.";
            return;
        }

        IsForgeAttentionRequired = false;
        ForgeAttentionMessage = string.Empty;
        ForgeNarrativeText = _currentForgePhase == ForgePhase.Error ? "Forge checkpoint dismissed." : ForgeNarrative.For(ForgePhase.Complete);
        StatusText = "Ready";
        StatusBrush = (Brush)FindResource("SuccessBrush");
        ScheduleForgeStatusAutoHide();
    }

    private string BuildForgeCompletionMessage()
    {
        if (_analysisSnapshot is null || ActiveProfileIssueCount == 0)
            return $"{SelectedProfile?.Name ?? "Profile"} forged successfully — no issues found.";

        var active = SelectedProfile?.ActiveMods.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var issues = _analysisSnapshot.Issues
            .Where(issue => active is null || active.Contains(issue.PackageId))
            .ToArray();
        var errors = issues.Count(issue => issue.Severity == AnalysisIssueSeverity.Error);
        var warnings = issues.Count(issue => issue.Severity == AnalysisIssueSeverity.Warning);
        var parts = new List<string>();
        if (errors > 0) parts.Add($"{errors} error{(errors == 1 ? string.Empty : "s")}");
        if (warnings > 0) parts.Add($"{warnings} warning{(warnings == 1 ? string.Empty : "s")}");
        var detail = parts.Count > 0 ? string.Join(" • ", parts) : $"{issues.Length} issue{(issues.Length == 1 ? string.Empty : "s")}";
        var first = issues.FirstOrDefault()?.Explanation;
        return string.IsNullOrWhiteSpace(first)
            ? $"The Forge found {issues.Length} issue{(issues.Length == 1 ? string.Empty : "s")}: {detail}."
            : $"The Forge found {issues.Length} issue{(issues.Length == 1 ? string.Empty : "s")}: {detail}. {first}";
    }

    private async void ScheduleForgeStatusAutoHide()
    {
        _forgeStatusHideCts?.Cancel();
        _forgeStatusHideCts?.Dispose();
        _forgeStatusHideCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2.5), _forgeStatusHideCts.Token);
            _forgeSessionService.Reset();
        }
        catch (OperationCanceledException) { }
    }

    private async Task CompleteFirstRunGuideAsync(bool hideNativeScan)
    {
        IsFirstRunGuideVisible = false;
        if (hideNativeScan)
            IsNativeScanVisible = false;
        _firstRunGuideCompleted = true;
        _firstRunGuideRevision = CurrentFirstRunGuideRevision;
        await SaveSettingsFeatureAsync("settings.save-onboarding", "Save Onboarding State");
    }

    private async void IgniteFirstRunForge_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteFeatureCommandAsync("Complete First-Run Guide", async () =>
        {
            if (!IsNativeScanSuccessful) return;
            await CompleteFirstRunGuideAsync(hideNativeScan: true);
            RunAudit_Click(sender, e);
        });
    }

    private async void SkipFirstRunGuide_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteFeatureCommandAsync(
            "Complete First-Run Guide",
            () => CompleteFirstRunGuideAsync(hideNativeScan: IsNativeScanComplete));
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (CancelFeatureTask())
        {
            _forgeSessionService.RequestCancellation();
            NativeProgressText = "Cancelling current operation";
        }
    }

    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        var output = SelectedProfile?.WorkspacePath ?? RuntimePaths.ProfilesRoot;
        Directory.CreateDirectory(output);
        Process.Start(new ProcessStartInfo("explorer.exe", output) { UseShellExecute = true });
    }

    private void OpenSelectedWorkshopBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod?.WorkshopUrl is null)
        {
            Append("The selected mod does not have a Workshop URL.");
            return;
        }

        Process.Start(new ProcessStartInfo(SelectedMod.WorkshopUrl) { UseShellExecute = true });
    }

    private void OpenSelectedWorkshopSteam_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedMod?.WorkshopId))
        {
            Append("The selected mod does not have a Workshop ID.");
            return;
        }

        var steamUri = $"steam://url/CommunityFilePage/{SelectedMod.WorkshopId}";
        try
        {
            Process.Start(new ProcessStartInfo(steamUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Append($"Steam could not be opened ({ex.Message}). Opening the Workshop page in the default browser instead.");
            if (!string.IsNullOrWhiteSpace(SelectedMod.WorkshopUrl))
            {
                Process.Start(new ProcessStartInfo(SelectedMod.WorkshopUrl) { UseShellExecute = true });
            }
        }
    }

    private void OpenSelectedFolder_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is null || !Directory.Exists(SelectedMod.RootPath)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", SelectedMod.RootPath) { UseShellExecute = true });
    }


    private async void RefreshProfiles_Click(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Refresh Profiles", LoadProfilesAsync);

    private async Task LoadProfilesAsync()
    {
        try
        {
            LoadProfileShellState();
            var installedMods = Mods.ToList();
            var loaded = await RunFeatureTaskAsync(
                "profile.load",
                "Load Profile Workspaces",
                context =>
                {
                    context.Report(new BackgroundTaskProgress(
                        "Loading profile workspaces",
                        "Reading saved profiles and validating installed mod references.",
                        RuntimePaths.ProfilesRoot,
                        null,
                        0,
                        0,
                        $"{installedMods.Count} installed mod(s) available",
                        RuntimePaths.ProfilesRoot));
                    return _profileWorkspaceService.LoadProfilesAsync(
                        RepositoryRoot,
                        installedMods,
                        context.CancellationToken);
                });
            var selectedName = SelectedProfile?.Name;
            var profileNames = loaded.Select(profile => profile.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var shellStateChanged = _favoriteProfileNames.RemoveWhere(name => !profileNames.Contains(name)) > 0;
            shellStateChanged |= _lockedProfileNames.RemoveWhere(name => !profileNames.Contains(name)) > 0;
            if (shellStateChanged) SaveProfileShellState();

            var ordered = loaded
                .Select(ApplyProfileShellState)
                .OrderByDescending(p => _favoriteProfileNames.Contains(p.Name))
                .ThenByDescending(p => p.IsBuiltIn)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Profiles.ReplaceAll(ordered);
            SelectedProfile = Profiles.FirstOrDefault(p => p.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase))
                              ?? Profiles.FirstOrDefault(p => !p.IsBuiltIn)
                              ?? Profiles.FirstOrDefault();
            Append($"Loaded {Profiles.Count} profile workspace(s).", ActivitySeverity.Success);
            await EnsureModsConfigMonitorAsync();
        }
        catch (OperationCanceledException)
        {
            Append("Profile loading cancelled.", ActivitySeverity.Warning);
            throw;
        }
        catch (Exception ex)
        {
            Append("Profile loading failed: " + ex.Message, ActivitySeverity.Error);
        }
    }

    private RimForgeProfile ApplyProfileShellState(RimForgeProfile profile)
    {
        var locked = profile.IsBuiltIn || _lockedProfileNames.Contains(profile.Name);
        return profile with { IsLocked = locked };
    }

    private void LoadProfileShellState()
    {
        _favoriteProfileNames.Clear();
        _lockedProfileNames.Clear();
        var state = _profileCatalogStateStore.Load(RuntimePaths.ProfilesRoot);
        _favoriteProfileNames.UnionWith(state.FavoriteProfileNames);
        _lockedProfileNames.UnionWith(state.LockedProfileNames);
    }

    private void SaveProfileShellState()
    {
        _profileCatalogStateStore.Save(
            RuntimePaths.ProfilesRoot,
            new ProfileCatalogState(
                _favoriteProfileNames.ToArray(),
                _lockedProfileNames.ToArray(),
                DateTimeOffset.UtcNow));
    }

    private static string MakeSafeProfileName(string name)
    {
        var cleaned = string.Concat(name.Trim().Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return string.IsNullOrWhiteSpace(cleaned) ? "New Profile" : cleaned;
    }

    private string? PromptForProfileName(string title, string initialValue)
        => ForgeDialogService.ShowPrompt(this, title, initialValue);

    private async void CreateProfile_Click(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Create Profile", CreateProfileAsync);

    private async Task CreateProfileAsync()
    {
        var name = PromptForProfileName("Create Empty Profile", "New Profile");
        if (string.IsNullOrWhiteSpace(name)) return;

        var result = await RunProfileOperationAsync(
            "profile.create",
            "Create Profile",
            $"Creating profile '{name}'.",
            RuntimePaths.ProfilesRoot,
            token => _profileWorkspaceService.CreateAsync(
                RepositoryRoot,
                name,
                new[] { "ludeon.rimworld" },
                "1.6",
                token));
        await CompleteProfileOperationAsync(result, selectResultProfile: true);
    }

    private async void RenameProfile_Click(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Rename Profile", RenameProfileAsync);

    private async Task RenameProfileAsync()
    {
        if (!CanRenameSelectedProfile || SelectedProfile is null) return;
        var profile = SelectedProfile;
        var newName = PromptForProfileName("Rename Profile", profile.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)) return;

        var result = await RunProfileOperationAsync(
            "profile.rename",
            "Rename Profile",
            $"Renaming '{profile.Name}' to '{newName}'.",
            profile.WorkspacePath,
            token => _profileWorkspaceService.RenameAsync(RepositoryRoot, profile, newName, token));
        if (result.Success && result.Profile is not null)
        {
            if (_favoriteProfileNames.Remove(profile.Name)) _favoriteProfileNames.Add(result.Profile.Name);
            if (_lockedProfileNames.Remove(profile.Name)) _lockedProfileNames.Add(result.Profile.Name);
            SaveProfileShellState();
        }
        await CompleteProfileOperationAsync(result, selectResultProfile: true);
    }

    private async void DuplicateProfile_Click(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Duplicate Profile", DuplicateProfileAsync);

    private async Task DuplicateProfileAsync()
    {
        if (SelectedProfile is null) return;
        var source = SelectedProfile;
        var name = PromptForProfileName("Duplicate Profile", source.Name + " Copy");
        if (string.IsNullOrWhiteSpace(name)) return;

        var result = await RunProfileOperationAsync(
            "profile.duplicate",
            "Duplicate Profile",
            $"Creating '{name}' from '{source.Name}'.",
            source.WorkspacePath,
            token => _profileWorkspaceService.DuplicateAsync(RepositoryRoot, source, name, token));
        await CompleteProfileOperationAsync(result, selectResultProfile: true);
    }

    private async void ImportProfile_Click(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Import Profile", ImportProfileAsync);

    private async Task ImportProfileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import RimForge Profile or ModsConfig.xml",
            Filter = "RimForge profiles (*.rfprofile.zip;*.xml)|*.rfprofile.zip;*.xml|Portable profile backups (*.rfprofile.zip)|*.rfprofile.zip|ModsConfig XML (*.xml)|*.xml|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        if (dialog.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var inspection = await _profilePackageInspectionService.InspectAsync(dialog.FileName, Mods.ToList());
            if (!inspection.CanImport)
            {
                var detail = inspection.Issues.FirstOrDefault() ?? "The package could not be verified.";
                Append("Profile package rejected: " + detail, ActivitySeverity.Error);
                _notificationService.Enqueue(new NotificationRequest(
                    "Profile package rejected", detail, NotificationSeverity.Error,
                    [new NotificationAction("view-activity", "View Details")]));
                return;
            }

            if (inspection.HasCompatibilityWarnings)
            {
                var warning = $"This profile references {inspection.MissingPackageIds.Count} missing and " +
                              $"{inspection.IncompatiblePackageIds.Count} version-incompatible mod(s). Import it anyway?";
                if (!ForgeDialogService.ShowConfirmation(this, "Profile Compatibility Warning", warning, "Import Anyway")) return;
            }
        }

        var defaultName = Path.GetFileNameWithoutExtension(dialog.FileName);
        var name = PromptForProfileName("Import Profile", defaultName);
        if (string.IsNullOrWhiteSpace(name)) return;

        var result = await RunProfileOperationAsync(
            "profile.import",
            "Import Profile",
            $"Importing profile '{name}'.",
            dialog.FileName,
            token => _profileWorkspaceService.ImportAsync(RepositoryRoot, dialog.FileName, name, token));
        await CompleteProfileOperationAsync(result, selectResultProfile: true);
    }

    private async void ExportProfile_Click(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Export Profile", ExportProfileAsync);

    private async Task ExportProfileAsync()
    {
        if (SelectedProfile is null || !File.Exists(SelectedProfile.ModsConfigPath)) return;
        var profile = SelectedProfile;
        var dialog = new SaveFileDialog
        {
            Title = "Export RimForge Profile",
            FileName = MakeSafeProfileName(profile.Name) + ".rfprofile.zip",
            DefaultExt = ".zip",
            Filter = "RimForge profile backup (*.rfprofile.zip)|*.rfprofile.zip|ModsConfig XML (*.xml)|*.xml"
        };
        if (dialog.ShowDialog(this) != true) return;

        var result = await RunProfileOperationAsync(
            "profile.export",
            "Export Profile",
            $"Exporting profile '{profile.Name}'.",
            dialog.FileName,
            token => _profileWorkspaceService.ExportAsync(profile, dialog.FileName, token));
        await CompleteProfileOperationAsync(result, selectResultProfile: false, reloadProfiles: false);
    }

    private void CompareProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is null || Profiles.Count < 2) return;

        var candidates = Profiles
            .Where(profile => !profile.Name.Equals(SelectedProfile.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(profile => _favoriteProfileNames.Contains(profile.Name))
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var suggested = candidates.FirstOrDefault()?.Name ?? string.Empty;
        var requestedName = PromptForProfileName("Compare Profile", suggested);
        if (string.IsNullOrWhiteSpace(requestedName)) return;

        var comparisonTarget = candidates.FirstOrDefault(profile =>
            profile.Name.Equals(requestedName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (comparisonTarget is null)
        {
            var available = string.Join(", ", candidates.Select(profile => profile.Name));
            var message = $"No saved profile named '{requestedName.Trim()}' was found. Available profiles: {available}.";
            Append(message, ActivitySeverity.Warning);
            _notificationService.Enqueue(new NotificationRequest(
                "Profile comparison unavailable",
                message,
                NotificationSeverity.Warning,
                new[] { new NotificationAction("view-activity", "View Details") },
                TimeSpan.FromSeconds(12)));
            return;
        }

        var comparison = _profileWorkspaceService.Compare(SelectedProfile, comparisonTarget);
        var summary = FormatProfileComparison(comparison);
        Append(summary, comparison.IsIdentical ? ActivitySeverity.Success : ActivitySeverity.Info);
        _notificationService.Enqueue(new NotificationRequest(
            comparison.IsIdentical ? "Profiles are identical" : "Profile comparison complete",
            summary,
            comparison.IsIdentical ? NotificationSeverity.Success : NotificationSeverity.Information,
            new[] { new NotificationAction("view-activity", "View Details") },
            TimeSpan.FromSeconds(15)));
    }

    private static string FormatProfileComparison(ProfileComparisonResult comparison)
    {
        if (comparison.IsIdentical)
            return $"'{comparison.LeftProfileName}' and '{comparison.RightProfileName}' contain the same mods in the same order.";

        var sections = new List<string>();
        if (comparison.AddedPackageIds.Count > 0)
            sections.Add($"Added in {comparison.RightProfileName}: {string.Join(", ", comparison.AddedPackageIds.Take(8))}" +
                         (comparison.AddedPackageIds.Count > 8 ? $" (+{comparison.AddedPackageIds.Count - 8} more)" : string.Empty));
        if (comparison.RemovedPackageIds.Count > 0)
            sections.Add($"Removed from {comparison.RightProfileName}: {string.Join(", ", comparison.RemovedPackageIds.Take(8))}" +
                         (comparison.RemovedPackageIds.Count > 8 ? $" (+{comparison.RemovedPackageIds.Count - 8} more)" : string.Empty));
        if (comparison.OrderChanges.Count > 0)
        {
            var moved = comparison.OrderChanges.Take(6)
                .Select(change => $"{change.PackageId} ({change.LeftIndex + 1}→{change.RightIndex + 1})");
            sections.Add($"Reordered: {string.Join(", ", moved)}" +
                         (comparison.OrderChanges.Count > 6 ? $" (+{comparison.OrderChanges.Count - 6} more)" : string.Empty));
        }

        return string.Join(" | ", sections);
    }

    private async void DeleteProfile_Click(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Delete Profile", DeleteProfileAsync);

    private async Task DeleteProfileAsync()
    {
        if (!CanDeleteSelectedProfile || SelectedProfile is null) return;
        var profile = SelectedProfile;
        if (!ForgeDialogService.ShowConfirmation(
                this,
                "Delete Profile",
                $"Delete profile '{profile.Name}'?\n\nRimForge will create a portable recovery backup before removing it.",
                "Delete",
                danger: true)) return;

        var result = await RunProfileOperationAsync(
            "profile.delete",
            "Delete Profile",
            $"Backing up and deleting profile '{profile.Name}'.",
            profile.WorkspacePath,
            token => _profileWorkspaceService.DeleteAsync(RepositoryRoot, profile, token));
        if (result.Success)
        {
            _favoriteProfileNames.Remove(profile.Name);
            _lockedProfileNames.Remove(profile.Name);
            SaveProfileShellState();
            SelectedProfile = null;
        }
        await CompleteProfileOperationAsync(result, selectResultProfile: false);
    }

    private async Task RestorePendingActivationRecoveryAsync()
    {
        var recoveryPath = _pendingActivationRecoveryPath;
        if (string.IsNullOrWhiteSpace(recoveryPath) || !File.Exists(recoveryPath))
        {
            _notificationService.Enqueue(new NotificationRequest(
                "Activation recovery unavailable",
                "The previous RimWorld mod configuration could not be found.",
                NotificationSeverity.Warning));
            return;
        }

        var result = await RunFeatureTaskAsync(
            "profile.restore-activation",
            "Restore Previous RimWorld Configuration",
            context =>
            {
                context.Report(new BackgroundTaskProgress(
                    "Restoring activation recovery",
                    "Restoring the previous ModsConfig.xml safely.",
                    recoveryPath,
                    null,
                    0,
                    0,
                    "Recovering the pre-activation profile state",
                    recoveryPath));
                return _profileWorkspaceService.RestoreActivationRecoveryAsync(recoveryPath, context.CancellationToken);
            });
        Append(result.Message, result.Success ? ActivitySeverity.Success : ActivitySeverity.Error);
        _notificationService.Enqueue(new NotificationRequest(
            result.Success ? "Previous configuration restored" : "Activation recovery failed",
            result.Message,
            result.Success ? NotificationSeverity.Success : NotificationSeverity.Error,
            [new NotificationAction("view-activity", result.Success ? "View Details" : "View Log")],
            result.Success ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(15)));
        if (result.Success) _pendingActivationRecoveryPath = null;
    }

    private async Task CompleteProfileOperationAsync(
        ProfileOperationResult result,
        bool selectResultProfile,
        bool reloadProfiles = true)
    {
        if (reloadProfiles && result.Success)
        {
            await LoadProfilesAsync();
            if (selectResultProfile && result.Profile is not null)
                SelectedProfile = Profiles.FirstOrDefault(profile => profile.Name.Equals(result.Profile.Name, StringComparison.OrdinalIgnoreCase));
        }

        var severity = result.Success ? NotificationSeverity.Success : NotificationSeverity.Error;
        var activitySeverity = result.Success ? ActivitySeverity.Success : ActivitySeverity.Error;
        var actions = new List<NotificationAction>();
        _pendingProfileExportPath = result.ExportPath;
        _pendingProfileBackupPath = result.CanRestore ? result.BackupPath : null;
        if (!string.IsNullOrWhiteSpace(_pendingProfileExportPath))
            actions.Add(new NotificationAction("open-profile-export", "Open Folder"));
        if (!string.IsNullOrWhiteSpace(_pendingProfileBackupPath))
            actions.Add(new NotificationAction("restore-profile-backup", "Restore"));
        actions.Add(new NotificationAction("view-activity", result.Success ? "View Details" : "View Log"));

        Append(result.Message, activitySeverity);
        _notificationService.Enqueue(new NotificationRequest(
            result.Success ? "Profile operation complete" : "Profile operation failed",
            result.Message,
            severity,
            actions,
            result.Success ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(15)));

        if (!result.Success)
            ForgeDialogService.ShowMessage(this, "Profile operation failed", result.Message, "Close");
    }

    private async void ToggleProfileLock_Click(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Update Profile Lock", ToggleProfileLockAsync);

    private async Task ToggleProfileLockAsync()
    {
        if (!CanLockSelectedProfile || SelectedProfile is null) return;
        if (_lockedProfileNames.Contains(SelectedProfile.Name))
            _lockedProfileNames.Remove(SelectedProfile.Name);
        else
            _lockedProfileNames.Add(SelectedProfile.Name);

        SaveProfileShellState();
        var selectedName = SelectedProfile.Name;
        await LoadProfilesAsync();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
        Append($"{SelectedProfile?.Name} is now {SelectedProfileStateText.ToLowerInvariant()}.", ActivitySeverity.Info);
    }

    private async void ToggleFavoriteProfile_Click(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Update Favorite Profile", ToggleFavoriteProfileAsync);

    private async Task ToggleFavoriteProfileAsync()
    {
        if (SelectedProfile is null) return;
        var selectedName = SelectedProfile.Name;
        if (_favoriteProfileNames.Contains(selectedName))
            _favoriteProfileNames.Remove(selectedName);
        else
            _favoriteProfileNames.Add(selectedName);

        SaveProfileShellState();
        await LoadProfilesAsync();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
        Notify(nameof(SelectedProfileFavoriteGlyph));
        Notify(nameof(IsSelectedProfileFavorite));
    }

    private void OpenProfileFolder_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is null) return;
        Directory.CreateDirectory(SelectedProfile.WorkspacePath);
        Process.Start(new ProcessStartInfo("explorer.exe", SelectedProfile.WorkspacePath) { UseShellExecute = true });
    }

    private async void ActivateProfile_Click(object sender, RoutedEventArgs e) =>
        await ExecuteFeatureCommandAsync("Activate Profile", ActivateProfileAsync);

    private async Task ActivateProfileAsync()
    {
        if (SelectedProfile is null) return;
        var profile = SelectedProfile;
        var result = await RunFeatureTaskAsync(
            "profile.activate",
            "Activate Profile",
            context =>
            {
                context.Report(new BackgroundTaskProgress(
                    "Activating profile",
                    $"Activating '{profile.Name}' for RimWorld.",
                    profile.ModsConfigPath,
                    null,
                    0,
                    profile.ActiveMods.Count,
                    $"{profile.ActiveMods.Count} active mod(s)",
                    profile.ModsConfigPath));
                return _profileWorkspaceService.ActivateAsync(profile, context.CancellationToken);
            });
        Append(result.Message, result.Success ? ActivitySeverity.Success : ActivitySeverity.Error);
        StatusText = result.Success ? $"{profile.Name} active" : "Activation failed";
        StatusBrush = (Brush)FindResource(result.Success ? "SuccessBrush" : "DangerBrush");

        _pendingActivationRecoveryPath = result.Success && !string.IsNullOrWhiteSpace(result.RecoveryPath)
            ? result.RecoveryPath
            : null;
        var actions = new List<NotificationAction>();
        if (_pendingActivationRecoveryPath is not null)
            actions.Add(new NotificationAction("restore-activation-recovery", "Restore Previous"));
        actions.Add(new NotificationAction("view-activity", result.Success ? "View Details" : "View Log"));
        _notificationService.Enqueue(new NotificationRequest(
            result.Success ? "Profile activated" : "Profile activation failed",
            result.Message,
            result.Success ? NotificationSeverity.Success : NotificationSeverity.Error,
            actions,
            result.Success ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(15)));
    }

    private bool _isProgrammaticWorkspaceScroll;
    private readonly Dictionary<ScrollViewer, double> _smoothScrollTargets = new();
    private readonly DispatcherTimer _smoothScrollTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(16)
    };


    private void ShowPage(UIElement page, string title)
    {
        // Compatibility bridge for existing actions such as “Show Issues”.
        // The application is now a continuous workspace rather than mutually exclusive pages.
        if (page is FrameworkElement section)
        {
            ScrollToWorkspaceSection(section, title);
        }
    }

    private string _activeWorkspaceParent = "Mod Sorter";

    private (FrameworkElement Element, string Title)[] GetWorkspaceSections() =>
    [
        (DashboardPanel, "Mod Sorter"),
        (ForgeViewPanel, "ForgeView"),
        (TextureToolsPanel, "Texture Tools"),
        (SettingsPanel, "Settings"),
        (ConsolePanel, "Console")
    ];

    private (FrameworkElement Element, string Parent, string Child, string Destination)[] GetWorkspaceLocations() =>
    [
        (ModSorterWorkspacePanel, "Mod Sorter", "Load Order", "Mod Sorter"),
        (IssueViewerWorkspacePanel, "Mod Sorter", "Issue Viewer", "Issue Viewer"),
        (ForgeViewPanel, "ForgeView", "Graph Workspace", "ForgeView"),
        (WorkspaceMetricsPanel, "ForgeView", "Engineering Metrics", "ForgeView"),
        (TextureToolsPanel, "Texture Tools", "Conversion Workspace", "Texture Tools"),
        (SettingsPanel, "Settings", "Workstation Configuration", "Settings"),
        (ConsolePanel, "Console", "Activity & Game Log", "Console")
    ];

    private void ScrollToWorkspaceSection(FrameworkElement section, string title, bool recordHistory = true)
    {
        if (!IsLoaded) return;

        _isProgrammaticWorkspaceScroll = true;
        var point = section.TransformToAncestor(WorkspaceSectionsPanel).Transform(new Point(0, 0));
        WorkspaceScrollViewer.ScrollToVerticalOffset(Math.Max(0, point.Y));
        var parentTitle = title == "Issue Viewer" ? "Mod Sorter" : title;
        PageTitle = parentTitle;
        _activeWorkspaceParent = parentTitle;
        UpdateNavigationLocation(title);
        if (recordHistory) RecordGlobalNavigationSnapshot();

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _isProgrammaticWorkspaceScroll = false;
            UpdateViewportNavigation();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void WorkspaceScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var viewportHeight = Math.Max(480, e.NewSize.Height);
        DashboardPanel.ClearValue(HeightProperty);
        DashboardPanel.MinHeight = viewportHeight;

        // The inactive library and active load-order lists together own exactly one
        // visible workstation viewport. They must never grow with their item counts;
        // both lists scroll internally while the statistics bar and Issue Viewer remain
        // below this fixed-height region in the outer workspace flow.
        var sorterViewportHeight = viewportHeight;
        ModSorterWorkspacePanel.Height = sorterViewportHeight;
        ModSorterWorkspacePanel.MinHeight = sorterViewportHeight;
        ModSorterWorkspacePanel.MaxHeight = sorterViewportHeight;
        ModSorterFeature.Height = sorterViewportHeight;
        ModSorterFeature.MinHeight = 0;
        ModSorterFeature.MaxHeight = sorterViewportHeight;

        // Issue Viewer sizes to its content. A forced percentage-height created a large
        // empty block below short issue lists and visually split the unified workspace.
        IssueViewerWorkspacePanel.ClearValue(HeightProperty);
        IssueViewerWorkspacePanel.MinHeight = 0;
        IssueViewerWorkspacePanel.MaxHeight = double.PositiveInfinity;
        WorkspaceMetricsPanel.ClearValue(HeightProperty);
        WorkspaceMetricsPanel.MinHeight = 132;

        foreach (var (element, _) in GetWorkspaceSections())
        {
            if (ReferenceEquals(element, DashboardPanel) || ReferenceEquals(element, SettingsPanel))
            {
                element.ClearValue(HeightProperty);
                element.MinHeight = viewportHeight;
                continue;
            }

            element.Height = viewportHeight;
            element.MinHeight = viewportHeight;
        }
    }

    private void WorkspaceScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0 || !IsLoaded) return;

        if (ForgeViewFeature?.OwnsGraphWheelInput(e.OriginalSource as DependencyObject) == true)
        {
            e.Handled = true;
            return;
        }

        var nestedScroller = FindNearestScrollViewer(e.OriginalSource as DependencyObject);
        var targetScroller = nestedScroller is not null && nestedScroller != WorkspaceScrollViewer && CanScroll(nestedScroller, e.Delta)
            ? nestedScroller
            : WorkspaceScrollViewer;

        if (!CanScroll(targetScroller, e.Delta)) return;

        e.Handled = true;
        var isGameLog = ConsoleFeature?.GameLogListControl is not null && ConsoleFeature.GameLogListControl.IsAncestorOf(targetScroller);
        var isIssueViewer = IssueViewerFeature?.ItemsList is not null && IssueViewerFeature.ItemsList.IsAncestorOf(targetScroller);
        var wheelScale = isGameLog ? 0.28 : isIssueViewer ? 0.20 : 0.55;
        QueueSmoothScroll(targetScroller, -e.Delta * wheelScale);
    }

    private void QueueSmoothScroll(ScrollViewer viewer, double delta)
    {
        var currentTarget = _smoothScrollTargets.TryGetValue(viewer, out var existing) ? existing : viewer.VerticalOffset;
        _smoothScrollTargets[viewer] = Math.Clamp(currentTarget + delta, 0, viewer.ScrollableHeight);
        if (!_smoothScrollTimer.IsEnabled) _smoothScrollTimer.Start();
    }

    private void SmoothScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (_smoothScrollTargets.Count == 0) { _smoothScrollTimer.Stop(); return; }
        foreach (var pair in _smoothScrollTargets.ToArray())
        {
            var viewer = pair.Key;
            var target = Math.Clamp(pair.Value, 0, viewer.ScrollableHeight);
            var difference = target - viewer.VerticalOffset;
            if (Math.Abs(difference) < 0.75)
            {
                viewer.ScrollToVerticalOffset(target);
                _smoothScrollTargets.Remove(viewer);
                continue;
            }
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset + (difference * 0.28));
        }
        if (_smoothScrollTargets.Count == 0) _smoothScrollTimer.Stop();
    }

    private static ScrollViewer? FindNearestScrollViewer(DependencyObject? source)
    {
        for (var current = source; current is not null; current = GetVisualOrLogicalParent(current))
            if (current is ScrollViewer viewer) return viewer;
        return null;
    }

    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject current)
    {
        if (current is System.Windows.Media.Visual || current is System.Windows.Media.Media3D.Visual3D)
            return VisualTreeHelper.GetParent(current);
        return LogicalTreeHelper.GetParent(current);
    }

    private static bool CanScroll(ScrollViewer viewer, int wheelDelta)
    {
        const double tolerance = 0.5;
        return wheelDelta < 0 ? viewer.VerticalOffset < viewer.ScrollableHeight - tolerance : viewer.VerticalOffset > tolerance;
    }

    private void WorkspaceScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isProgrammaticWorkspaceScroll || !IsLoaded) return;
        UpdateViewportNavigation();
    }

    private void UpdateViewportNavigation()
    {
        var viewportHeight = Math.Max(1, WorkspaceScrollViewer.ViewportHeight);
        var parentCandidates = GetWorkspaceSections()
            .Select(section => (section.Element, section.Title, Ratio: GetViewportVisibilityRatio(section.Element, viewportHeight)))
            .ToArray();

        var current = parentCandidates.FirstOrDefault(item => item.Title == _activeWorkspaceParent);
        var activationThreshold = 0.51;
        var retentionThreshold = 0.45;
        var winner = parentCandidates.OrderByDescending(item => item.Ratio).First();
        var parentTitle = current.Element is not null && current.Ratio >= retentionThreshold
            ? current.Title
            : winner.Ratio >= activationThreshold
                ? winner.Title
                : current.Element is not null ? current.Title : winner.Title;

        _activeWorkspaceParent = parentTitle;
        var child = GetWorkspaceLocations()
            .Where(location => location.Parent == parentTitle)
            .Select(location => (Location: location, Ratio: GetViewportVisibilityRatio(location.Element, viewportHeight)))
            .OrderByDescending(item => item.Ratio)
            .FirstOrDefault();

        var destination = child.Location.Element is not null ? child.Location.Destination : parentTitle;
        if (!string.Equals(PageTitle, parentTitle, StringComparison.Ordinal))
        {
            PageTitle = parentTitle;
            RecordGlobalNavigationSnapshot();
        }
        UpdateNavigationLocation(destination);
    }

    private double GetViewportVisibilityRatio(FrameworkElement element, double viewportHeight)
    {
        if (!element.IsVisible || element.ActualHeight <= 0) return 0;
        var top = element.TransformToAncestor(WorkspaceSectionsPanel).Transform(new Point(0, 0)).Y - WorkspaceScrollViewer.VerticalOffset;
        var bottom = top + element.ActualHeight;
        var visible = Math.Max(0, Math.Min(viewportHeight, bottom) - Math.Max(0, top));
        return visible / viewportHeight;
    }

    private void TextureTools_Click(object sender, RoutedEventArgs e) =>
        ScrollToWorkspaceSection(TextureToolsPanel, "Texture Tools");

    private void ToggleInspector_Click(object sender, RoutedEventArgs e)
    {
        var isOpen = InspectorColumn.Width.Value > 40;
        InspectorColumn.Width = isOpen ? new GridLength(40) : new GridLength(360);
        ModInspectorControl.SetExpanded(!isOpen);
    }

    private void EnableDarkTitleBar()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var enabled = 1;
            DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        }
        catch { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private bool FilterMod(object item)
    {
        if (item is not ModRecord mod) return false;
        return _modFilteringService.Matches(mod, CreateFilterCriteria());
    }

    private bool FilterSorterItem(object item)
    {
        if (item is not ModSorterItemViewModel sorterItem) return false;
        if (ShowIssuesOnly && sorterItem.AnalysisIssueCount == 0) return false;
        var criteria = CreateFilterCriteria() with { IssuesOnly = false };
        return _modFilteringService.Matches(sorterItem.Mod, criteria);
    }

    private ModFilterCriteria CreateFilterCriteria()
    {
        IReadOnlySet<string>? activePackageIds = SelectedProfile?.ActiveMods
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new ModFilterCriteria(SearchText, ShowFullLibrary, ShowIssuesOnly, activePackageIds, Mods.ToList(), StructuredSearchQuery.Parse(SearchText));
    }

    private void RebuildModSorter()
    {
        var previousId = SelectedMod?.Id;
        var activeLoadOrder = SelectedProfile?.ActiveMods.ToArray();
        // The installed library is authoritative. Analysis enriches this projection, but a
        // missing or failed analysis generation must never erase successfully discovered mods.
        var items = BuildModSorterItems(Mods, activeLoadOrder, _analysisSnapshot);

        ModSorterItems.ReplaceAll(items);

        SelectedSorterItem = previousId is null
            ? ModSorterItems.FirstOrDefault()
            : ModSorterItems.FirstOrDefault(item => item.Mod.Id == previousId) ?? ModSorterItems.FirstOrDefault();
        Notify(nameof(ModSorterScopeText));
    }



    private void QueueAnalysisRefresh()
    {
        var version = ++_analysisRefreshVersion;
        _analysisRefreshTask = RefreshAnalysisSnapshotAsync(version);
    }

    private async Task RefreshAnalysisSnapshotAsync(int version)
    {
        if (_isClosing) return;
        var mods = Mods.ToList();
        var loadOrder = SelectedProfile?.ActiveMods.ToArray();
        var profileName = SelectedProfile?.Name ?? "full library";
        var profilePath = SelectedProfile?.ModsConfigPath ?? RepositoryRoot;

        try
        {
            while (_backgroundTaskService.Current.IsActive)
            {
                if (_isClosing || version != _analysisRefreshVersion) return;
                if (IsFeatureTaskRunning("analysis.refresh"))
                    CancelFeatureTask("A newer analysis scope was selected.");
                await Task.Delay(25);
            }

            if (_isClosing || version != _analysisRefreshVersion) return;
            var snapshot = await RunFeatureTaskAsync(
                "analysis.refresh",
                "Refresh Workspace Analysis",
                async context =>
                {
                    context.Report(new BackgroundTaskProgress(
                        "Analyzing workspace",
                        $"Refreshing dependency and issue state for {profileName}.",
                        $"Analyzing {mods.Count} installed mod(s).",
                        null,
                        0,
                        Math.Max(1, mods.Count),
                        "Building Forge DNA, dependency, and issue projections",
                        profilePath));
                    var result = await _forgeDnaService.AnalyzeAsync(
                        mods,
                        loadOrder,
                        TargetRimWorldVersion,
                        _forgeEvidenceSnapshot.Contributions,
                        cancellationToken: context.CancellationToken).ConfigureAwait(false);
                    context.Report(new BackgroundTaskProgress(
                        "Analysis ready",
                        $"Workspace analysis for {profileName} is ready.",
                        $"Resolved {result.Analysis.Relationships.Count} relationship(s) and {result.Analysis.Issues.Count} issue(s).",
                        100,
                        mods.Count,
                        Math.Max(1, mods.Count),
                        "Shared analysis snapshot prepared",
                        profileName));
                    return result.Analysis;
                });

            if (_isClosing || version != _analysisRefreshVersion) return;
            _analysisSnapshot = snapshot;
            NotifyAnalysisProperties();
            RebuildIssueViewer();
            RebuildModSorter();
            RebuildProfileLoadOrder();
        }
        catch (OperationCanceledException)
        {
            // A newer profile selection or application shutdown superseded this refresh.
        }
        catch (Exception)
        {
            // RunFeatureTaskAsync has already logged and surfaced the failure.
        }
        finally
        {
            if (version == _analysisRefreshVersion)
                _analysisRefreshTask = null;
        }
    }


    private void RebuildProfileLoadOrder()
    {
        _undoService.Clear();
        if (SelectedProfile is null)
        {
            ActiveProfileMods.ReplaceAll(Array.Empty<ProfileLoadOrderItemViewModel>());
            var fallbackItems = BuildInactiveProfileItems(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            InactiveInstalledMods.ReplaceAll(fallbackItems);
            ProfileLoadOrderItems.ReplaceAll(fallbackItems);
            IsLoadOrderDirty = false;
            Notify(nameof(SelectedProfileModCountText));
            Notify(nameof(ModSorterScopeText));
            return;
        }

        var byPackage = Mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
            .GroupBy(mod => mod.PackageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(mod => mod.IsOfficialContent)
                    .ThenByDescending(mod => mod.HasWorkshop)
                    .ThenBy(mod => mod.Errors.Count)
                    .ThenByDescending(mod => mod.LastModified)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var activeIds = LoadOrderRules.Normalize(SelectedProfile.ActiveMods).ToList();

        if (SelectedProfile.IsBuiltIn)
        {
            var officialIds = Mods
                .Where(mod => mod.PackageId?.StartsWith("ludeon.rimworld", StringComparison.OrdinalIgnoreCase) == true)
                .Select(mod => mod.PackageId!)
                .Append("ludeon.rimworld")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id.Equals("ludeon.rimworld", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            activeIds = officialIds
                .Where(id => id.Equals("ludeon.rimworld", StringComparison.OrdinalIgnoreCase) || activeIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        var activeSet = activeIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activeItems = new List<ProfileLoadOrderItemViewModel>(activeIds.Count);
        var position = 1;
        foreach (var packageId in activeIds)
        {
            byPackage.TryGetValue(packageId, out var mod);
            var analysis = _analysisSnapshot?.GetSummary(packageId);
            activeItems.Add(new ProfileLoadOrderItemViewModel(
                position++, packageId, mod, true,
                analysis?.IssueCount ?? mod?.Errors.Count ?? 0,
                analysis?.IsInCycle ?? false,
                analysis?.HealthLabel));
        }

        var inactiveItems = BuildInactiveProfileItems(activeSet);

        ActiveProfileMods.ReplaceAll(activeItems);
        InactiveInstalledMods.ReplaceAll(inactiveItems);
        ProfileLoadOrderItems.ReplaceAll(activeItems.Concat(inactiveItems));
        IsLoadOrderDirty = false;
        Notify(nameof(SelectedProfileModCountText));
        Notify(nameof(ModSorterScopeText));
    }

    private ProfileLoadOrderItemViewModel[] BuildInactiveProfileItems(IReadOnlySet<string> activeSet) =>
        Mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId) && !activeSet.Contains(mod.PackageId!))
            .GroupBy(mod => mod.PackageId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(mod => mod.IsOfficialContent)
                .ThenByDescending(mod => mod.HasWorkshop)
                .ThenBy(mod => mod.Errors.Count)
                .ThenByDescending(mod => mod.LastModified)
                .First())
            .OrderBy(mod => mod.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(mod =>
            {
                var analysis = _analysisSnapshot?.GetSummary(mod.PackageId);
                return new ProfileLoadOrderItemViewModel(
                    0, mod.PackageId!, mod, false,
                    analysis?.IssueCount ?? mod.Errors.Count,
                    analysis?.IsInCycle ?? false,
                    analysis?.HealthLabel);
            })
            .ToArray();

    private async void StartWatchingGameLog_Click(object sender, RoutedEventArgs e)
    {
        GameLogEntries.Clear();
        _gameLogAutoFollow = false;
        _isLoadingOlderGameLogEntries = true;
        var playerLogPath = _gameLaunchService.GetDefaultPlayerLogPath();
        try
        {
            await RunFeatureTaskAsync(
                "console.start-log-watch",
                "Start Player Log Watch",
                async context =>
                {
                    context.Report(new BackgroundTaskProgress(
                        "Loading Player.log",
                        "Reading recent game log entries before live monitoring begins.",
                        playerLogPath,
                        null,
                        0,
                        500,
                        "Opening the RimWorld diagnostic stream",
                        playerLogPath));
                    await _gameLogService.StartAsync(playerLogPath, startAtEnd: true, context.CancellationToken);
                    context.Report(new BackgroundTaskProgress(
                        "Player.log ready",
                        "Live game log monitoring is active.",
                        playerLogPath,
                        100,
                        500,
                        500,
                        "Recent diagnostic history loaded",
                        playerLogPath));
                });
            _gameLogAutoFollow = true;
            if (GameLogEntries.Count > 0 && ConsoleFeature?.GameLogListControl is not null)
                ConsoleFeature.GameLogListControl.ScrollIntoView(GameLogEntries[^1]);
            Append("Started watching RimWorld Player.log.", ActivitySeverity.Success);
        }
        catch (OperationCanceledException)
        {
            Append("Player.log monitoring startup was cancelled.", ActivitySeverity.Warning);
        }
        catch (Exception)
        {
            // RunFeatureTaskAsync has already logged and surfaced the failure.
        }
        finally
        {
            _isLoadingOlderGameLogEntries = false;
        }
    }

    private async void GameLogList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ListBox listBox || e.OriginalSource is not ScrollViewer scrollViewer)
            return;

        if (!_isLoadingOlderGameLogEntries)
            _gameLogAutoFollow = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset <= 24;

        if (_isLoadingOlderGameLogEntries || scrollViewer.VerticalOffset > 24 || GameLogEntries.Count == 0)
            return;
        if (_backgroundTaskService.Current.IsActive)
            return;

        _isLoadingOlderGameLogEntries = true;
        try
        {
            var previousExtent = scrollViewer.ExtentHeight;
            var previousOffset = scrollViewer.VerticalOffset;
            var playerLogPath = _gameLogService.CurrentPath ?? _gameLaunchService.GetDefaultPlayerLogPath();
            var olderEntries = await RunFeatureTaskAsync(
                "console.load-log-history",
                "Load Earlier Player Log Entries",
                async context =>
                {
                    context.Report(new BackgroundTaskProgress(
                        "Loading earlier log entries",
                        "Reading the previous Player.log history window.",
                        playerLogPath,
                        null,
                        0,
                        500,
                        "Scanning the preceding diagnostic log window",
                        playerLogPath));
                    var entries = await _gameLogService.LoadPreviousAsync(cancellationToken: context.CancellationToken);
                    context.Report(new BackgroundTaskProgress(
                        "Earlier log entries loaded",
                        $"Loaded {entries.Count} earlier Player.log entries.",
                        playerLogPath,
                        100,
                        entries.Count,
                        Math.Max(1, entries.Count),
                        entries.Count == 0 ? "Beginning of log history reached" : "Earlier diagnostic entries discovered",
                        playerLogPath));
                    return entries;
                });
            if (olderEntries.Count == 0)
                return;

            for (var index = olderEntries.Count - 1; index >= 0; index--)
                GameLogEntries.Insert(0, olderEntries[index]);

            listBox.UpdateLayout();
            var addedExtent = Math.Max(0, scrollViewer.ExtentHeight - previousExtent);
            scrollViewer.ScrollToVerticalOffset(previousOffset + addedExtent);
        }
        catch (OperationCanceledException)
        {
            // Scrolling may be superseded by another feature operation.
        }
        catch (Exception)
        {
            // RunFeatureTaskAsync has already logged and surfaced the failure.
        }
        finally
        {
            _isLoadingOlderGameLogEntries = false;
        }
    }

    private async void StopWatchingGameLog_Click(object sender, RoutedEventArgs e)
    {
        await _gameLogService.StopAsync();
        Append("Stopped watching RimWorld Player.log.", ActivitySeverity.Info);
    }

    private void ClearGameLog_Click(object sender, RoutedEventArgs e)
    {
        GameLogEntries.Clear();
        _gameLogAutoFollow = true;
    }

    private bool FilterEdge(object item)
    {
        if (item is not DependencyGraphEdge edge) return false;
        if (!ShowFullLibrary && SelectedProfile is not null)
        {
            var active = SelectedProfile.ActiveMods.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!active.Contains(edge.SourceId) || !active.Contains(edge.TargetId)) return false;
        }

        var query = StructuredSearchQuery.Parse(SearchText);
        if (!query.IsValid) return false;
        if (query.IsEmpty) return true;

        return query.Evaluate(clause => clause.Field switch
        {
            "identity" or "mod" or "package" => Contains(edge.SourceId, clause.Value) || Contains(edge.TargetId, clause.Value),
            "requires" => Contains(edge.TargetId, clause.Value) && edge.Relationship is DependencyRelationshipType.Required or DependencyRelationshipType.Optional,
            "required-by" => Contains(edge.SourceId, clause.Value) && edge.Relationship is DependencyRelationshipType.Required or DependencyRelationshipType.Optional,
            "incompatible" => edge.Relationship == DependencyRelationshipType.Incompatible
                              && (Contains(edge.SourceId, clause.Value) || Contains(edge.TargetId, clause.Value) || Contains(edge.Description, clause.Value)),
            "issue" => Contains(edge.Description, clause.Value),
            _ => Contains(edge.SourceId, clause.Value) || Contains(edge.TargetId, clause.Value) || Contains(edge.Description, clause.Value)
        });
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private void TextureTools_ActivityRequested(object? sender, string message)
    {
        var severity = message.Contains("failed", StringComparison.OrdinalIgnoreCase)
            ? ActivitySeverity.Error
            : message.Contains("cancel", StringComparison.OrdinalIgnoreCase)
                ? ActivitySeverity.Warning
                : ActivitySeverity.Info;
        Append(message, severity);
    }

    private void Append(string line, ActivitySeverity? severity = null) => Dispatcher.Invoke(() =>
    {
        _log.AppendLine(line);
        Notify(nameof(LogText));

        var resolvedSeverity = severity ?? ResolveSeverity(line);
        ActivityEntries.Add(new ActivityEntry(DateTimeOffset.Now, resolvedSeverity, CleanActivityMessage(line)));
        while (ActivityEntries.Count > 500) ActivityEntries.RemoveAt(0);
    });

    private void ApplyForgeProgress(ForgeProgress progress, bool reportBackgroundTask = true)
    {
        if (reportBackgroundTask && _backgroundTaskService.IsRunning)
        {
            var detailLines = progress.TechnicalMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var currentFile = detailLines.LastOrDefault(line => Path.IsPathFullyQualified(line)) ?? string.Empty;
            _backgroundTaskService.Report(new BackgroundTaskProgress(
                progress.Phase.ToString(),
                ForgeNarrative.For(progress.Phase),
                progress.TechnicalMessage,
                progress.OverallPercentage,
                progress.Completed,
                progress.Total,
                detailLines.FirstOrDefault() ?? progress.Phase.ToString(),
                currentFile));
        }
        if (_nativeForgeRunner.IsRunning || ForgeSession.IsVisible) _forgeSessionService.Report(progress);
        _currentForgePhase = progress.Phase;
        ForgeNarrativeText = ForgeNarrative.For(progress.Phase);
        ForgeTechnicalMessage = progress.TechnicalMessage;
        ForgePurposeText = PurposeFor(progress.Phase);
        OverallProgressValue = progress.OverallPercentage;
        PhaseProgressValue = progress.PhasePercentage;
        IsPhaseIndeterminate = false;
        NativeProgressValue = progress.OverallPercentage;
        NativeProgressText = progress.TechnicalMessage;
    }

    private static string PurposeFor(ForgePhase phase) => phase switch
    {
        ForgePhase.Configuration => "Purpose: Loading paths, rules, and user preferences for a reproducible session.",
        ForgePhase.Discovery => "Purpose: Building an inventory of every installed mod and content source.",
        ForgePhase.AboutParsing => "Purpose: Collecting identity, dependency, version, and author metadata.",
        ForgePhase.Validation => "Purpose: Detecting malformed metadata and installation problems before analysis.",
        ForgePhase.IndexBuilding => "Purpose: Creating fast lookup indexes for relationships and package identities.",
        ForgePhase.DependencyGraph => "Purpose: Building the relationship graph used for impact and load-order analysis.",
        ForgePhase.EvidenceScan => "Purpose: Collecting objective XML, path, and assembly evidence for compatibility analysis.",
        ForgePhase.VersionChecks => "Purpose: Comparing supported game versions and available update evidence.",
        ForgePhase.ProfileProcessing => "Purpose: Evaluating the active workspace as a coherent modpack ecosystem.",
        ForgePhase.ReportGeneration => "Purpose: Turning analysis results into actionable reports and diagnostics.",
        ForgePhase.DatabaseGeneration => "Purpose: Persisting normalized knowledge for future scans and recommendations.",
        ForgePhase.Complete => "Purpose: The current modpack ecosystem is ready for review.",
        ForgePhase.Cancelled => "Purpose: The Forge Session was stopped before completion.",
        ForgePhase.Error => "Purpose: Preserve the failure context so the issue can be diagnosed safely.",
        _ => "Purpose: Preparing analysis context."
    };

    private void HandleAuditOutput(string line)
    {
        var phase = DetectForgePhase(line);
        if (phase is not null)
        {
            var overall = OverallForPhase(phase.Value);
            ApplyForgeProgress(new ForgeProgress(phase.Value, CleanActivityMessage(line), overall, 0));
            IsPhaseIndeterminate = phase is not ForgePhase.Complete and not ForgePhase.Error and not ForgePhase.Cancelled;
        }

        Append(line);
    }

    private static ForgePhase? DetectForgePhase(string line)
    {
        if (line.Contains("Configuration loaded", StringComparison.OrdinalIgnoreCase)) return ForgePhase.Configuration;
        if (line.Contains("Scanning ", StringComparison.OrdinalIgnoreCase) || line.Contains("Discovered ", StringComparison.OrdinalIgnoreCase)) return ForgePhase.Discovery;
        if (line.Contains("About metadata", StringComparison.OrdinalIgnoreCase) || line.Contains("About.xml", StringComparison.OrdinalIgnoreCase)) return ForgePhase.AboutParsing;
        if (line.Contains("Built indexes", StringComparison.OrdinalIgnoreCase)) return ForgePhase.IndexBuilding;
        if (line.Contains("Validation complete", StringComparison.OrdinalIgnoreCase)) return ForgePhase.Validation;
        if (line.Contains("Dependency graph", StringComparison.OrdinalIgnoreCase) || line.Contains("Dependency cycle", StringComparison.OrdinalIgnoreCase)) return ForgePhase.DependencyGraph;
        if (line.Contains("Evidence scan", StringComparison.OrdinalIgnoreCase) || line.Contains("Scanning mod XML", StringComparison.OrdinalIgnoreCase)) return ForgePhase.EvidenceScan;
        if (line.Contains("Version status", StringComparison.OrdinalIgnoreCase) || line.Contains("Workshop status", StringComparison.OrdinalIgnoreCase)) return ForgePhase.VersionChecks;
        if (line.Contains("Processing profile", StringComparison.OrdinalIgnoreCase) || line.Contains("Loaded profile", StringComparison.OrdinalIgnoreCase)) return ForgePhase.ProfileProcessing;
        if (line.Contains("Building generated mod database", StringComparison.OrdinalIgnoreCase) || line.Contains("Generated database", StringComparison.OrdinalIgnoreCase)) return ForgePhase.DatabaseGeneration;
        if (line.Contains("Audit written", StringComparison.OrdinalIgnoreCase) || line.Contains("reports written", StringComparison.OrdinalIgnoreCase)) return ForgePhase.ReportGeneration;
        if (line.Contains("Startup complete", StringComparison.OrdinalIgnoreCase)) return ForgePhase.Complete;
        if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("failed", StringComparison.OrdinalIgnoreCase)) return ForgePhase.Error;
        return null;
    }

    private static double OverallForPhase(ForgePhase phase) => phase switch
    {
        ForgePhase.Configuration => 0.03,
        ForgePhase.Discovery => 0.08,
        ForgePhase.AboutParsing => 0.18,
        ForgePhase.Validation => 0.25,
        ForgePhase.IndexBuilding => 0.30,
        ForgePhase.DependencyGraph => 0.34,
        ForgePhase.EvidenceScan => 0.58,
        ForgePhase.VersionChecks => 0.70,
        ForgePhase.ProfileProcessing => 0.82,
        ForgePhase.DatabaseGeneration => 0.92,
        ForgePhase.ReportGeneration => 0.97,
        ForgePhase.Complete => 1,
        _ => 0
    };

    private static ActivitySeverity ResolveSeverity(string line)
    {
        if (line.Contains("[SUCCESS]", StringComparison.OrdinalIgnoreCase) || line.Contains("complete", StringComparison.OrdinalIgnoreCase)) return ActivitySeverity.Success;
        if (line.Contains("[WARNING]", StringComparison.OrdinalIgnoreCase) || line.Contains("[RECOVERABLE]", StringComparison.OrdinalIgnoreCase) || line.Contains("warning", StringComparison.OrdinalIgnoreCase)) return ActivitySeverity.Warning;
        if (line.Contains("[FATAL]", StringComparison.OrdinalIgnoreCase) || line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("failed", StringComparison.OrdinalIgnoreCase)) return ActivitySeverity.Error;
        return ActivitySeverity.Info;
    }

    private static string CleanActivityMessage(string line)
    {
        var message = line.Trim();
        var firstBracket = message.IndexOf(']');
        if (message.StartsWith("[", StringComparison.Ordinal) && firstBracket >= 0)
        {
            message = message[(firstBracket + 1)..].TrimStart();
            if (message.StartsWith("[", StringComparison.Ordinal))
            {
                var secondBracket = message.IndexOf(']');
                if (secondBracket >= 0) message = message[(secondBracket + 1)..].TrimStart();
            }
        }
        if (message.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase)) message = message[6..].TrimStart();
        return message;
    }

    private void LoadLegacySummary()
    {
        try
        {
            var path = Path.Combine(RuntimePaths.OutputRoot, "Advanced", "Audit.json");
            if (!File.Exists(path)) path = Path.Combine(RuntimePaths.OutputRoot, "Audit.json");
            // Compatibility fallback for reports created by older RimForge builds.
            if (!File.Exists(path)) path = Path.Combine(RepositoryRoot, "Output", "Advanced", "Audit.json");
            if (!File.Exists(path)) path = Path.Combine(RepositoryRoot, "Output", "Audit.json");
            if (!File.Exists(path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            var graph = root.GetProperty("DependencyGraph");
            var validation = root.GetProperty("Validation");
            Summary = new AuditSummary(
                root.GetProperty("ModCount").GetInt32(),
                graph.GetProperty("MissingDependencyCount").GetInt32(),
                graph.GetProperty("CycleCount").GetInt32(),
                validation.GetProperty("MissingNames").GetArrayLength() + validation.GetProperty("MissingPackageIds").GetArrayLength(),
                root.TryGetProperty("Generated", out var generated) && DateTimeOffset.TryParse(generated.GetString(), out var date) ? date : null);
        }
        catch (Exception ex)
        {
            Append("Could not read Audit.json: " + ex.Message);
        }
    }

    private sealed class DispatcherThrottledProgress<T> : IProgress<T>, IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<T> _handler;
        private readonly object _sync = new();
        private readonly System.Threading.Timer _timer;
        private T _latest = default!;
        private bool _hasLatest;
        private bool _disposed;

        public DispatcherThrottledProgress(
            Dispatcher dispatcher,
            Action<T> handler,
            TimeSpan interval)
        {
            _dispatcher = dispatcher;
            _handler = handler;
            _timer = new System.Threading.Timer(
                static state => ((DispatcherThrottledProgress<T>)state!).Flush(),
                this,
                interval,
                interval);
        }

        public void Report(T value)
        {
            lock (_sync)
            {
                if (_disposed) return;
                _latest = value;
                _hasLatest = true;
            }
        }

        private void Flush()
        {
            T value;
            lock (_sync)
            {
                if (_disposed || !_hasLatest) return;
                value = _latest;
                _hasLatest = false;
            }

            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished) return;
            _dispatcher.BeginInvoke(DispatcherPriority.Background, _handler, value);
        }

        public void Dispose()
        {
            T value = default!;
            var shouldFlush = false;
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                if (_hasLatest)
                {
                    value = _latest;
                    shouldFlush = true;
                    _hasLatest = false;
                }
            }

            _timer.Dispose();
            if (shouldFlush && !_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
            {
                _dispatcher.BeginInvoke(DispatcherPriority.Background, _handler, value);
            }
        }
    }

    private static string? FindRepositoryRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Config.json"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(name!);
        return true;
    }
}
