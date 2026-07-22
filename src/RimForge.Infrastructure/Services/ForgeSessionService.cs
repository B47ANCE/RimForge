using System.Text.Json;
using System.Text.Json.Serialization;
using RimForge.Core.Models;
using RimForge.Core.Services;
using RimForge.Core.Diagnostics;

namespace RimForge.Infrastructure.Services;

public sealed class ForgeSessionService : IForgeSessionService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private readonly IApplicationEventBus? _eventBus;
    private readonly string? _sessionsRoot;
    private readonly IDiagnosticService? _diagnostics;
    private readonly ISessionLog? _sessionLog;
    private CancellationTokenSource? _cancellation;
    private ForgeSession? _session;
    private ForgeSessionSnapshot _current = ForgeSessionSnapshot.Idle;

    public ForgeSessionService(
        IApplicationEventBus? eventBus = null,
        string? sessionsRoot = null,
        IDiagnosticService? diagnostics = null,
        ISessionLog? sessionLog = null)
    {
        _eventBus = eventBus;
        _sessionsRoot = string.IsNullOrWhiteSpace(sessionsRoot) ? null : Path.GetFullPath(sessionsRoot);
        _diagnostics = diagnostics;
        _sessionLog = sessionLog;
        RestoreLatest();
    }

    public ForgeSession? CurrentSession { get { lock (_gate) return _session; } }
    public ForgeSessionSnapshot Current { get { lock (_gate) return _current; } }
    public CancellationToken CancellationToken { get { lock (_gate) return _cancellation?.Token ?? System.Threading.CancellationToken.None; } }
    public event EventHandler<ForgeSessionSnapshot>? SessionChanged;

    public ForgeSession Start(ForgeSessionRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.GameVersion);
        if (request.ModCount < 0) throw new ArgumentOutOfRangeException(nameof(request.ModCount));

        ForgeSession session;
        lock (_gate)
        {
            if (_session?.IsActive == true)
                throw new InvalidOperationException($"Forge session {_session.Id} is already active.");

            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();
            session = new ForgeSession(
                ForgeSessionId.New(),
                ForgeSessionState.Starting,
                DateTimeOffset.UtcNow,
                null,
                Path.GetFullPath(request.Workspace),
                request.ProfileName,
                request.GameVersion,
                request.ModCount,
                ForgeRuntimeStatus.NotStarted,
                "Starting",
                request.Message,
                "Preparing the selected profile for analysis.",
                0,
                0,
                false);
            _session = session;
            _sessionLog?.BeginSession(session.Id.Value);
        }

        Publish(session);
        return session;
    }

    public void Report(ForgeProgress progress)
    {
        ForgeSession updated;
        lock (_gate)
        {
            var current = RequireSession();
            if (!current.IsActive) return;
            var state = progress.Phase switch
            {
                ForgePhase.Complete => ForgeSessionState.Completed,
                ForgePhase.Error => ForgeSessionState.Failed,
                ForgePhase.Cancelled => ForgeSessionState.Cancelled,
                _ when current.State == ForgeSessionState.Cancelling => ForgeSessionState.Cancelling,
                _ => ForgeSessionState.Running
            };
            updated = current with
            {
                State = state,
                Stage = progress.Phase.ToString(),
                Message = progress.TechnicalMessage,
                Purpose = ForgeNarrative.PurposeFor(progress.Phase),
                OverallProgress = Math.Clamp((int)Math.Round(progress.OverallProgress * 100), 0, 100),
                StageProgress = Math.Clamp((int)Math.Round(progress.PhaseProgress * 100), 0, 100),
                IsStageIndeterminate = progress.PhaseProgress < 0,
                CompletedUtc = IsTerminal(state) ? DateTimeOffset.UtcNow : null
            };
            _session = updated;
        }
        Publish(updated);
    }

    public void SetRuntimeStatus(ForgeRuntimeStatus status, string? message = null)
    {
        ForgeSession updated;
        lock (_gate)
        {
            var current = RequireSession();
            updated = current with
            {
                RuntimeStatus = status,
                Message = string.IsNullOrWhiteSpace(message) ? current.Message : message
            };
            _session = updated;
        }
        Publish(updated);
    }

    public bool RequestCancellation(string message = "Forge session cancellation requested.")
    {
        ForgeSession updated;
        lock (_gate)
        {
            if (_session is null || !_session.IsActive || _session.State == ForgeSessionState.Cancelling) return false;
            updated = _session with { State = ForgeSessionState.Cancelling, Stage = "Cancelling", Message = message };
            _session = updated;
            _cancellation?.Cancel();
        }
        Publish(updated);
        return true;
    }

    public void Complete(string message) => Finish(ForgeSessionState.Completed, "Complete", message, null, 100, 100);
    public void Fail(string message, Exception? exception = null) => Finish(ForgeSessionState.Failed, "Failed", message, exception?.ToString() ?? message);
    public void Cancel(string message = "Forge session cancelled.") => Finish(ForgeSessionState.Cancelled, "Cancelled", message);

    public void Reset()
    {
        lock (_gate)
        {
            if (_session?.IsActive == true) throw new InvalidOperationException("An active Forge session cannot be reset.");
            _session = null;
            _current = ForgeSessionSnapshot.Idle;
            _cancellation?.Dispose();
            _cancellation = null;
        }
        PublishSnapshot(ForgeSessionSnapshot.Idle);
    }

    private void Finish(ForgeSessionState state, string stage, string message, string? error = null, int? overall = null, int? stageProgress = null)
    {
        ForgeSession updated;
        lock (_gate)
        {
            var current = RequireSession();
            updated = current with
            {
                State = state,
                Stage = stage,
                Message = message,
                Error = error,
                OverallProgress = overall ?? current.OverallProgress,
                StageProgress = stageProgress ?? current.StageProgress,
                IsStageIndeterminate = false,
                CompletedUtc = DateTimeOffset.UtcNow
            };
            _session = updated;
        }
        Publish(updated);
    }

    private ForgeSession RequireSession() => _session ?? throw new InvalidOperationException("No Forge session is active.");
    private static bool IsTerminal(ForgeSessionState state) => state is ForgeSessionState.Completed or ForgeSessionState.Failed or ForgeSessionState.Cancelled;

    private void Publish(ForgeSession session)
    {
        Persist(session);
        _diagnostics?.Write(
            session.State is ForgeSessionState.Failed ? RimForgeLogLevel.Error : RimForgeLogLevel.Information,
            "ForgeSession",
            session.Message,
            operationId: session.Stage,
            sessionId: session.Id.Value,
            properties: new Dictionary<string, string>
            {
                ["state"] = session.State.ToString(),
                ["progress"] = session.OverallProgress.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["runtimeStatus"] = session.RuntimeStatus.ToString()
            });
        var snapshot = ToSnapshot(session);
        lock (_gate) _current = snapshot;
        PublishSnapshot(snapshot);
        if (IsTerminal(session.State)) _sessionLog?.EndSession(session.Id.Value);
    }

    private void PublishSnapshot(ForgeSessionSnapshot snapshot)
    {
        SessionChanged?.Invoke(this, snapshot);
        _eventBus?.Publish(new ForgeSessionChangedEvent(snapshot));
    }

    private static ForgeSessionSnapshot ToSnapshot(ForgeSession session) => new(
        session.State switch
        {
            ForgeSessionState.Starting or ForgeSessionState.Running => ForgeSessionStatus.Running,
            ForgeSessionState.Cancelling => ForgeSessionStatus.Cancelling,
            ForgeSessionState.Completed => ForgeSessionStatus.Completed,
            ForgeSessionState.Failed => ForgeSessionStatus.Failed,
            ForgeSessionState.Cancelled => ForgeSessionStatus.Cancelled,
            _ => ForgeSessionStatus.Idle
        },
        session.ProfileName,
        session.Stage,
        session.Message,
        session.Purpose,
        session.OverallProgress,
        session.StageProgress,
        session.IsStageIndeterminate,
        session.StartedUtc,
        session.CompletedUtc,
        session.Error,
        session.Id,
        session.Workspace,
        session.GameVersion,
        session.ModCount,
        session.RuntimeStatus);

    private void Persist(ForgeSession session)
    {
        if (_sessionsRoot is null) return;
        Directory.CreateDirectory(_sessionsRoot);
        var sessionDirectory = Path.Combine(_sessionsRoot, session.Id.Value);
        Directory.CreateDirectory(sessionDirectory);
        WriteAtomic(Path.Combine(sessionDirectory, "session.json"), session);
        WriteAtomic(Path.Combine(_sessionsRoot, "current.json"), session);
    }

    private static void WriteAtomic(string path, ForgeSession session)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(session, JsonOptions));
        File.Move(temporary, path, true);
    }

    private void RestoreLatest()
    {
        if (_sessionsRoot is null) return;
        var path = Path.Combine(_sessionsRoot, "current.json");
        if (!File.Exists(path)) return;
        try
        {
            var restored = JsonSerializer.Deserialize<ForgeSession>(File.ReadAllText(path), JsonOptions);
            if (restored is null) return;
            if (restored.IsActive)
            {
                restored = restored with
                {
                    State = ForgeSessionState.Failed,
                    Stage = "Interrupted",
                    Message = "The previous Forge session ended before completion.",
                    Error = "Application shutdown or process interruption detected.",
                    CompletedUtc = DateTimeOffset.UtcNow
                };
                Persist(restored);
            }
            _session = restored;
            _current = ToSnapshot(restored);
        }
        catch (JsonException)
        {
            // A corrupt recovery marker must not prevent RimForge from starting.
        }
        catch (IOException)
        {
            // Session recovery is best-effort; the next session will replace the marker.
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }
}
