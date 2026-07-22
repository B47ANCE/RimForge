using System.Text.Json;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class ApplicationRecoveryService : IApplicationRecoveryService
{
    private sealed record RunMarker(string RunId, string ApplicationVersion, DateTimeOffset StartedAtUtc);
    private readonly string _markerPath;
    private string? _currentRunId;

    public ApplicationRecoveryService(string recoveryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryRoot);
        _markerPath = Path.Combine(Path.GetFullPath(recoveryRoot), "active-run.json");
    }

    public async Task<ApplicationRecoveryState> BeginRunAsync(string applicationVersion, CancellationToken cancellationToken = default)
    {
        RunMarker? interrupted = null;
        if (File.Exists(_markerPath))
        {
            try
            {
                await using var input = File.OpenRead(_markerPath);
                interrupted = await JsonSerializer.DeserializeAsync<RunMarker>(input, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch { QuarantineMarker(); }
        }

        _currentRunId = Guid.NewGuid().ToString("N");
        var marker = new RunMarker(_currentRunId, applicationVersion, DateTimeOffset.UtcNow);
        Directory.CreateDirectory(Path.GetDirectoryName(_markerPath)!);
        var temporary = _markerPath + ".tmp";
        await using (var output = File.Create(temporary))
            await JsonSerializer.SerializeAsync(output, marker, cancellationToken: cancellationToken).ConfigureAwait(false);
        File.Move(temporary, _markerPath, true);
        return new(interrupted is null, _currentRunId, interrupted?.RunId, interrupted?.StartedAtUtc, _markerPath);
    }

    public Task CompleteRunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_currentRunId is null) return Task.CompletedTask;
        if (File.Exists(_markerPath)) File.Delete(_markerPath);
        _currentRunId = null;
        return Task.CompletedTask;
    }

    private void QuarantineMarker()
    {
        try { File.Move(_markerPath, _markerPath + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}", false); }
        catch { }
    }
}
