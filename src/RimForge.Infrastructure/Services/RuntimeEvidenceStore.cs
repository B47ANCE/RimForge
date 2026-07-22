using System.Text.Json;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class RuntimeEvidenceStore : IRuntimeEvidenceStore
{
    private readonly string _path;
    private readonly ICompatibilityIntelligenceService _intelligence;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RuntimeEvidenceSnapshot _current = RuntimeEvidenceSnapshot.Empty;

    public RuntimeEvidenceStore(string path, ICompatibilityIntelligenceService intelligence)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _intelligence = intelligence ?? throw new ArgumentNullException(nameof(intelligence));
    }

    public RuntimeEvidenceSnapshot Current => Volatile.Read(ref _current);
    public event EventHandler<RuntimeEvidenceSnapshot>? SnapshotChanged;

    public async Task<RuntimeEvidenceSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path)) return Publish(RuntimeEvidenceSnapshot.Empty);
            await using var stream = File.OpenRead(_path);
            var persisted = await JsonSerializer.DeserializeAsync<PersistedState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? new PersistedState();
            return Publish(BuildSnapshot(persisted.Sessions, persisted.Evidence));
        }
        catch (JsonException)
        {
            var quarantine = _path + ".corrupt-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            File.Move(_path, quarantine, overwrite: true);
            return Publish(RuntimeEvidenceSnapshot.Empty);
        }
        finally { _gate.Release(); }
    }

    public async Task BeginSessionAsync(RuntimeEvidenceSession session, CancellationToken cancellationToken = default)
    {
        await MutateAsync(state =>
        {
            state.Sessions.RemoveAll(item => string.Equals(item.SessionId, session.SessionId, StringComparison.OrdinalIgnoreCase));
            state.Sessions.Add(session);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task EndSessionAsync(string sessionId, DateTimeOffset endedUtc, string reason, CancellationToken cancellationToken = default)
    {
        await MutateAsync(state =>
        {
            var index = state.Sessions.FindIndex(item => string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) state.Sessions[index] = state.Sessions[index] with { EndedUtc = endedUtc, EndReason = reason };
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task IngestAsync(IEnumerable<RuntimeEvidenceRecord> evidence, CancellationToken cancellationToken = default)
    {
        var incoming = evidence?.ToArray() ?? [];
        if (incoming.Length == 0) return;
        await MutateAsync(state =>
        {
            foreach (var item in incoming)
            {
                var index = state.Evidence.FindIndex(existing => SameEvidence(existing, item));
                if (index < 0) state.Evidence.Add(item);
                else
                {
                    var existing = state.Evidence[index];
                    state.Evidence[index] = existing with
                    {
                        LastObservedUtc = existing.LastObservedUtc > item.LastObservedUtc ? existing.LastObservedUtc : item.LastObservedUtc,
                        FirstObservedUtc = existing.FirstObservedUtc < item.FirstObservedUtc ? existing.FirstObservedUtc : item.FirstObservedUtc,
                        OccurrenceCount = existing.OccurrenceCount + Math.Max(1, item.OccurrenceCount),
                        Confidence = Math.Max(existing.Confidence, item.Confidence),
                        Detail = string.IsNullOrWhiteSpace(item.Detail) ? existing.Detail : item.Detail,
                        StackTrace = string.IsNullOrWhiteSpace(item.StackTrace) ? existing.StackTrace : item.StackTrace
                    };
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task MutateAsync(Action<PersistedState> mutation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = new PersistedState { Sessions = Current.Sessions.ToList(), Evidence = Current.Evidence.ToList() };
            mutation(state);
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            var temp = _path + ".tmp";
            await using (var stream = File.Create(temp))
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
            File.Move(temp, _path, overwrite: true);
            Publish(BuildSnapshot(state.Sessions, state.Evidence));
        }
        finally { _gate.Release(); }
    }

    private RuntimeEvidenceSnapshot BuildSnapshot(IReadOnlyList<RuntimeEvidenceSession> sessions, IReadOnlyList<RuntimeEvidenceRecord> evidence) =>
        new(sessions.OrderByDescending(item => item.StartedUtc).ToArray(),
            evidence.OrderByDescending(item => item.LastObservedUtc).ToArray(),
            _intelligence.Evaluate(evidence), DateTimeOffset.UtcNow);

    private RuntimeEvidenceSnapshot Publish(RuntimeEvidenceSnapshot snapshot)
    {
        Volatile.Write(ref _current, snapshot);
        SnapshotChanged?.Invoke(this, snapshot);
        return snapshot;
    }

    private static bool SameEvidence(RuntimeEvidenceRecord left, RuntimeEvidenceRecord right) =>
        string.Equals(left.SessionId, right.SessionId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Fingerprint, right.Fingerprint, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.SourcePackageId, right.SourcePackageId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.TargetPackageId, right.TargetPackageId, StringComparison.OrdinalIgnoreCase);

    private sealed class PersistedState
    {
        public List<RuntimeEvidenceSession> Sessions { get; set; } = [];
        public List<RuntimeEvidenceRecord> Evidence { get; set; } = [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
