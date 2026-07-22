using System.IO.Pipes;
using System.Text;
using RimForge.Core.BackgroundTasks;
using RimForge.Core.Models;
using RimForge.Core.Services;
using RimForge.Protocol;
using RimForge.Protocol.Contracts;
using RimForge.Protocol.Serialization;

namespace RimForge.Infrastructure.Services;

public sealed class RuntimeSensorHost : IRuntimeSensorHost
{
    private readonly IRuntimeEvidenceStore _store;
    private readonly IHostedBackgroundWorkService _hostedWork;
    private const string WorkKey = "runtime-sensor";
    public RuntimeSensorHost(IRuntimeEvidenceStore store, IHostedBackgroundWorkService hostedWork)
    {
        _store = store;
        _hostedWork = hostedWork;
    }
    public bool IsListening => _hostedWork.Snapshot.Any(item => item.Key == WorkKey && item.IsActive);
    public event EventHandler<bool>? ListeningChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsListening) return;
        await _hostedWork.StartAsync(WorkKey, "Runtime sensor pipe", ListenLoopAsync, cancellationToken).ConfigureAwait(false);
        ListeningChanged?.Invoke(this, true);
    }

    public async Task StopAsync()
    {
        await _hostedWork.StopAsync(WorkKey).ConfigureAwait(false);
        ListeningChanged?.Invoke(this, false);
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(ProtocolConstants.PipeName, PipeDirection.In, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server, new UTF8Encoding(false), false, 4096, leaveOpen: true);
                while (server.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null) break;
                    await HandleAsync(ProtocolSerializer.Deserialize(line), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception) { await Task.Delay(500, cancellationToken).ConfigureAwait(false); }
        }
    }

    private async Task HandleAsync(RimForgeEnvelope envelope, CancellationToken cancellationToken)
    {
        if (string.Equals(envelope.MessageType, MessageTypes.SessionStarted, StringComparison.Ordinal))
        {
            var payload = envelope.Payload.ToObject<RuntimeSessionStartedPayload>() ?? new RuntimeSessionStartedPayload();
            await _store.BeginSessionAsync(new RuntimeEvidenceSession(envelope.SessionId, envelope.TimestampUtc, null,
                payload.AgentVersion, payload.GameVersion, payload.ProfileName, payload.EnvironmentFingerprint,
                payload.ActivePackageIds.ToArray(), payload.ActiveDlcPackageIds.ToArray()), cancellationToken).ConfigureAwait(false);
        }
        else if (string.Equals(envelope.MessageType, MessageTypes.RuntimeEvidence, StringComparison.Ordinal))
        {
            var payload = envelope.Payload.ToObject<RuntimeEvidencePayload>();
            if (payload is not null) await _store.IngestAsync([Map(envelope, payload)], cancellationToken).ConfigureAwait(false);
        }
        else if (string.Equals(envelope.MessageType, MessageTypes.RuntimeEvidenceBatch, StringComparison.Ordinal))
        {
            var payload = envelope.Payload.ToObject<RuntimeEvidenceBatchPayload>();
            if (payload is not null) await _store.IngestAsync(payload.Evidence.Select(item => Map(envelope, item)), cancellationToken).ConfigureAwait(false);
        }
        else if (string.Equals(envelope.MessageType, MessageTypes.SessionEnded, StringComparison.Ordinal))
        {
            var payload = envelope.Payload.ToObject<SessionEndedPayload>() ?? new SessionEndedPayload();
            await _store.EndSessionAsync(envelope.SessionId, envelope.TimestampUtc, payload.Reason, cancellationToken).ConfigureAwait(false);
        }
    }

    private static RuntimeEvidenceRecord Map(RimForgeEnvelope envelope, RuntimeEvidencePayload payload) => new(
        payload.EvidenceId, envelope.SessionId, payload.Fingerprint, payload.Kind, payload.RelationshipKind,
        ParseSeverity(payload.Severity), Math.Clamp(payload.Confidence, 0, 1), payload.SourcePackageId,
        payload.TargetPackageId, payload.SourceAssembly, payload.TargetAssembly, payload.Title, payload.Summary,
        payload.Detail, payload.ExceptionType, payload.StackTrace, payload.Provenance,
        payload.FirstObservedUtc, payload.LastObservedUtc, Math.Max(1, payload.OccurrenceCount),
        RuntimeEvidenceDisposition.Observed, new Dictionary<string, string>(payload.Attributes, StringComparer.OrdinalIgnoreCase));

    private static RuntimeEvidenceSeverity ParseSeverity(string value) => value?.ToLowerInvariant() switch
    {
        "critical" or "fatal" => RuntimeEvidenceSeverity.Critical,
        "error" => RuntimeEvidenceSeverity.Error,
        "warning" or "warn" => RuntimeEvidenceSeverity.Warning,
        "trace" or "debug" => RuntimeEvidenceSeverity.Trace,
        _ => RuntimeEvidenceSeverity.Information
    };

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
