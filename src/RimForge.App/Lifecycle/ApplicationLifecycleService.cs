using RimForge.Core.Services;

namespace RimForge.App.Lifecycle;

public enum ApplicationLifecycleState
{
    Created,
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed
}

public sealed record ApplicationLifecycleSnapshot(
    ApplicationLifecycleState State,
    DateTimeOffset ChangedUtc,
    string Stage,
    string Detail,
    Exception? Error = null)
{
    public static ApplicationLifecycleSnapshot Created { get; } = new(
        ApplicationLifecycleState.Created,
        DateTimeOffset.UtcNow,
        "Composition",
        "Application services have not started yet.");
}

public interface IApplicationLifecycleService
{
    ApplicationLifecycleSnapshot Current { get; }
    event EventHandler<ApplicationLifecycleSnapshot>? StateChanged;
    void Transition(ApplicationLifecycleState state, string stage, string detail, Exception? error = null);
}

public sealed class ApplicationLifecycleService(IApplicationEventBus eventBus) : IApplicationLifecycleService
{
    private readonly object _gate = new();
    private ApplicationLifecycleSnapshot _current = ApplicationLifecycleSnapshot.Created;

    public ApplicationLifecycleSnapshot Current
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler<ApplicationLifecycleSnapshot>? StateChanged;

    public void Transition(ApplicationLifecycleState state, string stage, string detail, Exception? error = null)
    {
        ApplicationLifecycleSnapshot snapshot;
        lock (_gate)
        {
            if (_current.State == ApplicationLifecycleState.Stopped && state != ApplicationLifecycleState.Stopped)
                throw new InvalidOperationException("A stopped RimForge lifecycle cannot be restarted.");

            snapshot = new ApplicationLifecycleSnapshot(state, DateTimeOffset.UtcNow, stage, detail, error);
            _current = snapshot;
        }

        StateChanged?.Invoke(this, snapshot);
        eventBus.Publish(new ApplicationLifecycleChangedEvent(snapshot));
    }
}

public sealed record ApplicationLifecycleChangedEvent(ApplicationLifecycleSnapshot Snapshot);
