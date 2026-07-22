namespace RimForge.Core.Services;

/// <summary>
/// Typed, process-local application event bus used for cross-feature notifications.
/// Publishers expose facts; subscribers decide how to react without taking a direct
/// dependency on the publishing feature.
/// </summary>
public interface IApplicationEventBus
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;
    void Publish<TEvent>(TEvent applicationEvent) where TEvent : notnull;
}

public sealed class ApplicationEventBus : IApplicationEventBus, IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<Delegate>> _subscriptions = new();
    private bool _disposed;

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var eventType = typeof(TEvent);
            if (!_subscriptions.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Delegate>();
                _subscriptions[eventType] = handlers;
            }
            handlers.Add(handler);
        }

        return new Subscription(() => Unsubscribe(handler));
    }

    public void Publish<TEvent>(TEvent applicationEvent) where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);

        Action<TEvent>[] handlers;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_subscriptions.TryGetValue(typeof(TEvent), out var registered) || registered.Count == 0)
                return;
            handlers = registered.Cast<Action<TEvent>>().ToArray();
        }

        foreach (var handler in handlers)
            handler(applicationEvent);
    }

    private void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        lock (_gate)
        {
            var eventType = typeof(TEvent);
            if (!_subscriptions.TryGetValue(eventType, out var handlers))
                return;
            handlers.Remove(handler);
            if (handlers.Count == 0)
                _subscriptions.Remove(eventType);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _subscriptions.Clear();
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;
        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
