namespace RimForge.Core.Services;

public enum ApplicationStatusKind
{
    Ready,
    Loading,
    Scanning,
    Forging,
    Cancelling,
    Error
}

public sealed record ApplicationStatusSnapshot(
    ApplicationStatusKind Kind,
    string Label,
    string Detail,
    DateTimeOffset UpdatedUtc)
{
    public static ApplicationStatusSnapshot Ready(string detail = "Workstation ready") =>
        new(ApplicationStatusKind.Ready, "Ready", detail, DateTimeOffset.UtcNow);
}

public interface IApplicationStatusService
{
    ApplicationStatusSnapshot Current { get; }
    event EventHandler<ApplicationStatusSnapshot>? Changed;
    void Set(ApplicationStatusKind kind, string label, string detail);
    void SetReady(string detail = "Workstation ready");
}

public sealed class ApplicationStatusService : IApplicationStatusService
{
    private readonly object _sync = new();
    private ApplicationStatusSnapshot _current = ApplicationStatusSnapshot.Ready();

    public ApplicationStatusSnapshot Current
    {
        get { lock (_sync) return _current; }
    }

    public event EventHandler<ApplicationStatusSnapshot>? Changed;

    public void Set(ApplicationStatusKind kind, string label, string detail)
    {
        var snapshot = new ApplicationStatusSnapshot(kind, label, detail, DateTimeOffset.UtcNow);
        lock (_sync) _current = snapshot;
        Changed?.Invoke(this, snapshot);
    }

    public void SetReady(string detail = "Workstation ready") =>
        Set(ApplicationStatusKind.Ready, "Ready", detail);
}
