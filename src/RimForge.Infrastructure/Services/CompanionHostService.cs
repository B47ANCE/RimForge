using System.Diagnostics;
using RimForge.Core.Models;
using RimForge.Core.Services;
using RimForge.Protocol;
using RimForge.Core.Diagnostics;

namespace RimForge.Infrastructure.Services;

public sealed class CompanionHostService : ICompanionHost
{
    private readonly IDiagnosticService? _diagnostics;
    private readonly object _gate = new();
    private Process? _process;
    private CompanionHostProcessSnapshot _current = CompanionHostProcessSnapshot.Stopped;

    public CompanionHostProcessSnapshot Current { get { lock (_gate) return _current; } }
    public event EventHandler<CompanionHostProcessSnapshot>? StateChanged;

    public CompanionHostService(IDiagnosticService? diagnostics = null) => _diagnostics = diagnostics;

    public Task<CompanionHostProcessSnapshot> StartAsync(
        CompanionHostLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_process is { HasExited: false })
            {
                if (_current.ForgeSessionId == request.ForgeSessionId) return Task.FromResult(_current);
                throw new InvalidOperationException("A Companion Host is already running for another Forge session.");
            }
        }

        Publish(new CompanionHostProcessSnapshot(
            CompanionHostProcessState.Starting, null, request.ForgeSessionId,
            DateTimeOffset.UtcNow, null, "Starting Companion Host."));

        try
        {
            var executable = ResolveExecutable(request.ExecutablePath);
            var arguments = BuildArguments(request);
            ProcessStartInfo startInfo;
            if (executable.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                startInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.ArgumentList.Add(executable);
            }
            else
            {
                startInfo = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Companion Host process did not start.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.EnableRaisingEvents = true;
            process.Exited += ProcessExited;
            lock (_gate) _process = process;
            Publish(new CompanionHostProcessSnapshot(
                CompanionHostProcessState.Running, process.Id, request.ForgeSessionId,
                DateTimeOffset.UtcNow, null, "Companion Host is running."));
        }
        catch (Exception exception)
        {
            Publish(new CompanionHostProcessSnapshot(
                CompanionHostProcessState.Faulted, null, request.ForgeSessionId,
                _current.StartedUtc, DateTimeOffset.UtcNow,
                "Companion Host failed to start.", exception.Message));
        }

        return Task.FromResult(Current);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Process? process;
        lock (_gate) process = _process;
        if (process is null || process.HasExited)
        {
            Publish(CompanionHostProcessSnapshot.Stopped);
            return;
        }

        Publish(Current with { State = CompanionHostProcessState.Stopping, Message = "Stopping Companion Host." });
        try
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            process.Dispose();
            lock (_gate) _process = null;
            Publish(Current with
            {
                State = CompanionHostProcessState.Stopped,
                ProcessId = null,
                StoppedUtc = DateTimeOffset.UtcNow,
                Message = "Companion Host stopped."
            });
        }
    }

    private static string ResolveExecutable(string? explicitPath)
    {
        var candidates = new[]
        {
            explicitPath,
            Path.Combine(AppContext.BaseDirectory, "RimForge.Companion.Host.exe"),
            Path.Combine(AppContext.BaseDirectory, "RimForge.Companion.Host.dll")
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            ?? throw new FileNotFoundException("RimForge.Companion.Host executable was not found.");
    }

    private static IReadOnlyList<string> BuildArguments(CompanionHostLaunchRequest request)
    {
        var arguments = new List<string>
        {
            "--session", request.ForgeSessionId.Value,
            "--state-root", Path.GetFullPath(request.StateRoot),
            "--pipe", request.PipeName ?? ProtocolConstants.PipeName
        };
        if (!string.IsNullOrWhiteSpace(request.PlayerLogPath))
        {
            arguments.Add("--player-log");
            arguments.Add(Path.GetFullPath(request.PlayerLogPath));
        }
        if (request.RimWorldProcessId is not null)
        {
            arguments.Add("--rimworld-pid");
            arguments.Add(request.RimWorldProcessId.Value.ToString());
        }
        return arguments;
    }

    private void ProcessExited(object? sender, EventArgs eventArgs)
    {
        var process = sender as Process;
        Publish(Current with
        {
            State = process?.ExitCode == 0 ? CompanionHostProcessState.Stopped : CompanionHostProcessState.Faulted,
            ProcessId = null,
            StoppedUtc = DateTimeOffset.UtcNow,
            Message = process?.ExitCode == 0 ? "Companion Host exited." : "Companion Host exited unexpectedly.",
            Error = process?.ExitCode == 0 ? null : $"Exit code {process?.ExitCode}."
        });
    }

    private void Publish(CompanionHostProcessSnapshot snapshot)
    {
        lock (_gate) _current = snapshot;
        _diagnostics?.Write(
            snapshot.State == CompanionHostProcessState.Faulted ? RimForgeLogLevel.Error : RimForgeLogLevel.Information,
            "CompanionHost",
            snapshot.Message,
            operationId: snapshot.State.ToString(),
            sessionId: snapshot.ForgeSessionId?.Value,
            properties: snapshot.ProcessId is null ? null : new Dictionary<string, string> { ["processId"] = snapshot.ProcessId.Value.ToString() });
        _diagnostics?.ReportHealth(new RuntimeHealth(
            snapshot.State switch
            {
                CompanionHostProcessState.Running => HealthStatus.Healthy,
                CompanionHostProcessState.Faulted => HealthStatus.Unhealthy,
                CompanionHostProcessState.Starting or CompanionHostProcessState.Stopping => HealthStatus.Degraded,
                _ => HealthStatus.Unknown
            },
            "CompanionHost",
            snapshot.Message,
            DateTimeOffset.UtcNow,
            snapshot.Error));
        StateChanged?.Invoke(this, snapshot);
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
