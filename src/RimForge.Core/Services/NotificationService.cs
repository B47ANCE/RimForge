using RimForge.Core.BackgroundTasks;
using RimForge.Core.Models;

namespace RimForge.Core.Services;

public enum NotificationSeverity
{
    Information,
    Success,
    Warning,
    Error
}

public sealed record NotificationAction(string Id, string Label);

public sealed record NotificationRequest(
    string Title,
    string Message,
    NotificationSeverity Severity = NotificationSeverity.Information,
    IReadOnlyList<NotificationAction>? Actions = null,
    TimeSpan? Duration = null,
    string? Id = null)
{
    public string NotificationId { get; init; } = string.IsNullOrWhiteSpace(Id)
        ? Guid.NewGuid().ToString("N")
        : Id;

    public IReadOnlyList<NotificationAction> AvailableActions { get; init; } = Actions ?? Array.Empty<NotificationAction>();
    public TimeSpan DisplayDuration { get; init; } = Duration ?? TimeSpan.FromSeconds(7);
}

public sealed record NotificationSnapshot(
    NotificationRequest? Current,
    int QueuedCount,
    bool IsProgressReserved)
{
    public static NotificationSnapshot Empty { get; } = new(null, 0, false);
    public bool IsVisible => Current is not null && !IsProgressReserved;
}

public sealed record NotificationRequestedEvent(NotificationRequest Notification);
public sealed record NotificationChangedEvent(NotificationSnapshot Snapshot);
public sealed record NotificationActionInvokedEvent(string NotificationId, string ActionId);

public interface INotificationService : IDisposable
{
    NotificationSnapshot Current { get; }
    void Enqueue(NotificationRequest notification);
    void DismissCurrent();
    void InvokeAction(string actionId);
    void Clear();
}

/// <summary>
/// Central non-modal application notification queue. Active Forge/background progress
/// reserves the shared Control Center communication surface; queued notifications resume
/// automatically when progress releases it.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly object _gate = new();
    private readonly IApplicationEventBus _eventBus;
    private readonly List<QueuedNotification> _queue = new();
    private readonly IDisposable[] _subscriptions;
    private QueuedNotification? _current;
    private Timer? _dismissTimer;
    private long _sequence;
    private bool _backgroundTaskReserved;
    private bool _forgeReserved;
    private bool _progressReserved;
    private bool _disposed;

    public NotificationService(IApplicationEventBus eventBus)
    {
        _eventBus = eventBus;
        _subscriptions =
        [
            eventBus.Subscribe<NotificationRequestedEvent>(e => Enqueue(e.Notification)),
            eventBus.Subscribe<BackgroundTaskChangedEvent>(e => SetBackgroundTaskState(e.Snapshot)),
            eventBus.Subscribe<ForgeSessionChangedEvent>(e => SetForgeState(e.Snapshot))
        ];
    }

    public NotificationSnapshot Current
    {
        get
        {
            lock (_gate)
                return SnapshotUnsafe();
        }
    }

    public void Enqueue(NotificationRequest notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var queued = new QueuedNotification(notification, GetPriority(notification.Severity), _sequence++);
            if (_current is not null && queued.Priority > _current.Priority)
            {
                _queue.Add(_current);
                _current = queued;
                SortQueueUnsafe();
                if (!_progressReserved) StartTimerUnsafe();
            }
            else
            {
                _queue.Add(queued);
                SortQueueUnsafe();
                PromoteNextUnsafe();
            }
            PublishUnsafe();
        }
    }

    public void DismissCurrent()
    {
        lock (_gate)
        {
            if (_disposed || _current is null) return;
            CancelTimerUnsafe();
            _current = null;
            PromoteNextUnsafe();
            PublishUnsafe();
        }
    }

    public void InvokeAction(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId)) return;
        NotificationRequest notification;
        lock (_gate)
        {
            if (_disposed || _current is null) return;
            notification = _current.Notification;
        }

        _eventBus.Publish(new NotificationActionInvokedEvent(notification.NotificationId, actionId));
        DismissCurrent();
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (_disposed) return;
            CancelTimerUnsafe();
            _current = null;
            _queue.Clear();
            PublishUnsafe();
        }
    }

    private void SetBackgroundTaskState(BackgroundTaskSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _backgroundTaskReserved = snapshot.IsActive || snapshot.State == BackgroundTaskState.Cancelling;
            UpdateProgressReservationUnsafe();
        }
    }

    private void SetForgeState(ForgeSessionSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _forgeReserved = snapshot.Status is ForgeSessionStatus.Running or ForgeSessionStatus.Cancelling;
            UpdateProgressReservationUnsafe();
        }
    }

    private void UpdateProgressReservationUnsafe()
    {
        var wasReserved = _progressReserved;
        _progressReserved = _backgroundTaskReserved || _forgeReserved;
        if (wasReserved == _progressReserved) return;
        if (_progressReserved)
            CancelTimerUnsafe();
        else
        {
            PromoteNextUnsafe();
            StartTimerUnsafe();
        }
        PublishUnsafe();
    }

    private void PromoteNextUnsafe()
    {
        if (_current is not null || _queue.Count == 0) return;
        _current = _queue[0];
        _queue.RemoveAt(0);
        if (!_progressReserved)
            StartTimerUnsafe();
    }

    private void StartTimerUnsafe()
    {
        CancelTimerUnsafe();
        if (_current is null || _current.Notification.DisplayDuration <= TimeSpan.Zero || _progressReserved) return;
        _dismissTimer = new Timer(_ => DismissCurrent(), null, _current.Notification.DisplayDuration, Timeout.InfiniteTimeSpan);
    }

    private void CancelTimerUnsafe()
    {
        _dismissTimer?.Dispose();
        _dismissTimer = null;
    }

    private void SortQueueUnsafe() => _queue.Sort(static (left, right) =>
    {
        var priority = right.Priority.CompareTo(left.Priority);
        return priority != 0 ? priority : left.Sequence.CompareTo(right.Sequence);
    });

    private NotificationSnapshot SnapshotUnsafe() => new(_current?.Notification, _queue.Count, _progressReserved);
    private void PublishUnsafe() => _eventBus.Publish(new NotificationChangedEvent(SnapshotUnsafe()));

    private static int GetPriority(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Error => 400,
        NotificationSeverity.Warning => 300,
        NotificationSeverity.Success => 200,
        _ => 100
    };

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            CancelTimerUnsafe();
            _queue.Clear();
            _current = null;
        }
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
    }

    private sealed record QueuedNotification(NotificationRequest Notification, int Priority, long Sequence);
}
