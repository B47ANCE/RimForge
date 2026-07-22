namespace RimForge.Companion.Host;

public sealed class CompanionHost : IAsyncDisposable
{
    private readonly CompanionHostOptions _options;
    private readonly IpcServer _ipc;
    private readonly SessionBridge _bridge;
    private readonly PlayerLogWatcher? _logWatcher;
    private readonly RuntimeProcessMonitor? _processMonitor;
    private CancellationTokenSource? _lifetime;
    private CompanionHostHealth _health;

    public CompanionHost(CompanionHostOptions options)
    {
        _options = options;
        _ipc = new IpcServer(options.PipeName);
        _bridge = new SessionBridge(options.StateRoot, options.ForgeSessionId);
        _logWatcher = string.IsNullOrWhiteSpace(options.PlayerLogPath) ? null : new PlayerLogWatcher(options.PlayerLogPath);
        _processMonitor = options.RimWorldProcessId is null ? null : new RuntimeProcessMonitor(options.RimWorldProcessId.Value);
        _health = CreateHealth(CompanionHealthStatus.Starting, "Companion Host created.");
        _ipc.EnvelopeReceived += (_, envelope) => { _bridge.Accept(envelope); PublishHealth(CompanionHealthStatus.Healthy, "Runtime envelope received."); };
        _ipc.ConnectionChanged += (_, connected) => PublishHealth(
            connected ? CompanionHealthStatus.Healthy : CompanionHealthStatus.Degraded,
            connected ? "Runtime Agent connected." : "Runtime Agent disconnected.");
        if (_processMonitor is not null)
            _processMonitor.RunningChanged += (_, running) => PublishHealth(
                running ? CompanionHealthStatus.Healthy : CompanionHealthStatus.Stopping,
                running ? "RimWorld process detected." : "RimWorld process exited.");
    }

    public CompanionHostHealth Health => _health;
    public string EvidencePath => _bridge.EvidencePath;
    public event EventHandler<CompanionHostHealth>? HealthChanged;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_lifetime is not null) throw new InvalidOperationException("Companion Host is already running.");
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _lifetime.Token;
        PublishHealth(CompanionHealthStatus.Starting, "Starting Companion Host services.");
        var tasks = new List<Task> { _ipc.RunAsync(token) };
        if (_logWatcher is not null) tasks.Add(_logWatcher.RunAsync(token));
        if (_processMonitor is not null) tasks.Add(_processMonitor.RunAsync(token));
        PublishHealth(CompanionHealthStatus.Healthy, "Companion Host is running.");
        try
        {
            if (_processMonitor is null)
                await Task.WhenAll(tasks).ConfigureAwait(false);
            else
            {
                await tasks[^1].ConfigureAwait(false);
                _lifetime.Cancel();
                try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch (OperationCanceledException) { }
            }
        }
        finally
        {
            PublishHealth(CompanionHealthStatus.Stopped, "Companion Host stopped.");
        }
    }

    public void RequestStop()
    {
        PublishHealth(CompanionHealthStatus.Stopping, "Companion Host stop requested.");
        _lifetime?.Cancel();
    }

    private CompanionHostHealth CreateHealth(CompanionHealthStatus status, string message) => new(
        status, message, DateTimeOffset.UtcNow, _ipc.IsListening, _ipc.IsConnected,
        _processMonitor?.IsRunning == true, _ipc.EnvelopesReceived, _ipc.RejectedEnvelopes);

    private void PublishHealth(CompanionHealthStatus status, string message)
    {
        _health = CreateHealth(status, message);
        HealthChanged?.Invoke(this, _health);
    }

    public async ValueTask DisposeAsync()
    {
        RequestStop();
        _lifetime?.Dispose();
        _lifetime = null;
        _bridge.Dispose();
        await Task.CompletedTask;
    }
}
