using RimForge.Core.Models;

namespace RimForge.Core.Services;

public interface IRuntimeEvidenceStore
{
    RuntimeEvidenceSnapshot Current { get; }
    event EventHandler<RuntimeEvidenceSnapshot>? SnapshotChanged;
    Task BeginSessionAsync(RuntimeEvidenceSession session, CancellationToken cancellationToken = default);
    Task EndSessionAsync(string sessionId, DateTimeOffset endedUtc, string reason, CancellationToken cancellationToken = default);
    Task IngestAsync(IEnumerable<RuntimeEvidenceRecord> evidence, CancellationToken cancellationToken = default);
    Task<RuntimeEvidenceSnapshot> LoadAsync(CancellationToken cancellationToken = default);
}

public interface ICompatibilityIntelligenceService
{
    IReadOnlyList<CompatibilityIntelligence> Evaluate(IEnumerable<RuntimeEvidenceRecord> evidence);
}

public interface IRuntimeSensorHost : IAsyncDisposable
{
    bool IsListening { get; }
    event EventHandler<bool>? ListeningChanged;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
