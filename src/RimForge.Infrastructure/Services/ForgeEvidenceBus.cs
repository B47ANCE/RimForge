namespace RimForge.Infrastructure.Services;

public enum ForgeEvidencePublicationReason
{
    Refreshed,
    Ingested,
    Restored
}

public sealed record ForgeEvidencePublication(
    ForgeEvidenceSnapshot Snapshot,
    ForgeEvidencePublicationReason Reason,
    DateTimeOffset PublishedAtUtc);

public interface IForgeEvidenceBus
{
    ForgeEvidenceSnapshot Current { get; }
    event EventHandler<ForgeEvidencePublication>? Published;
    void Publish(ForgeEvidenceSnapshot snapshot, ForgeEvidencePublicationReason reason);
}

public sealed class ForgeEvidenceBus : IForgeEvidenceBus
{
    private readonly object _gate = new();
    private ForgeEvidenceSnapshot _current = ForgeEvidenceSnapshot.Empty;

    public ForgeEvidenceSnapshot Current
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler<ForgeEvidencePublication>? Published;

    public void Publish(ForgeEvidenceSnapshot snapshot, ForgeEvidencePublicationReason reason)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (snapshot.Generation < _current.Generation)
                throw new InvalidOperationException(
                    $"Evidence generation {snapshot.Generation} cannot replace generation {_current.Generation}.");
            _current = snapshot;
        }

        Published?.Invoke(this, new ForgeEvidencePublication(snapshot, reason, DateTimeOffset.UtcNow));
    }
}
