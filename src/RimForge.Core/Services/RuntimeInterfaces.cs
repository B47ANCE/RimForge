using RimForge.Core.Models;

namespace RimForge.Core.Services;

public interface IForgeSessionManager
{
    ForgeSession? CurrentSession { get; }
    ForgeSessionSnapshot Current { get; }
    CancellationToken CancellationToken { get; }
    event EventHandler<ForgeSessionSnapshot>? SessionChanged;
    ForgeSession Start(ForgeSessionRequest request);
    void Report(ForgeProgress progress);
    void SetRuntimeStatus(ForgeRuntimeStatus status, string? message = null);
    bool RequestCancellation(string message = "Forge session cancellation requested.");
    void Complete(string message);
    void Fail(string message, Exception? exception = null);
    void Cancel(string message = "Forge session cancelled.");
    void Reset();
}

public interface IForgeSessionService : IForgeSessionManager;

public interface IGameLogService : IAsyncDisposable
{
    bool IsWatching { get; }
    string? CurrentPath { get; }
    event EventHandler<GameLogEntry>? EntryReceived;
    event EventHandler<GameLogReplaySummary>? StartupReplayCompleted;
    event EventHandler<bool>? WatchingChanged;
    Task StartAsync(string playerLogPath, bool startAtEnd = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameLogEntry>> LoadPreviousAsync(int maxLines = 500, int maxBytes = 262144, CancellationToken cancellationToken = default);
    Task StopAsync();
}

public interface IGameLaunchService
{
    Task<GameLaunchResult> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default);
    string GetDefaultPlayerLogPath();
}

public interface ICompanionHost : IAsyncDisposable
{
    CompanionHostProcessSnapshot Current { get; }
    event EventHandler<CompanionHostProcessSnapshot>? StateChanged;
    Task<CompanionHostProcessSnapshot> StartAsync(
        CompanionHostLaunchRequest request,
        CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
