namespace Plugin.Maui.Intercom;

partial class IntercomImplementation
{
    private readonly Lock _unreadObservableGate = new();
    private IObservable<int>? _unreadObservable;

    // Built lazily and shared across getter calls so every subscriber rides one native
    // listener. The property itself lives per-platform: Android and iOS return this, the
    // .net fallback throws, keeping "every fallback member throws" intact.
    private IObservable<int> GetUnreadConversationCounts()
    {
        lock (_unreadObservableGate)
        {
            return _unreadObservable ??= new UnreadConversationCountObservable(
                () => UnreadConversationCount,
                handler => UnreadConversationCountChanged += handler,
                handler => UnreadConversationCountChanged -= handler);
        }
    }
}

/// <summary>
///     A <c>BehaviorSubject</c>-style observable over the unread conversation count: replays
///     the current value to each new subscriber, then forwards every change.
/// </summary>
/// <remarks>
///     Hand-rolled rather than pulling in System.Reactive — the plugin keeps a deliberately
///     small dependency graph, and this is the only reactive surface. Driven by delegates
///     rather than an <see cref="IIntercom" /> reference so it is testable off-device without
///     a platform.
/// </remarks>
internal sealed class UnreadConversationCountObservable : IObservable<int>
{
    private readonly Lock _gate = new();
    private readonly List<IObserver<int>> _observers = [];
    private readonly Func<int> _currentCount;
    private readonly Action<EventHandler<int>> _addHandler;
    private readonly Action<EventHandler<int>> _removeHandler;
    private bool _attached;

    public UnreadConversationCountObservable(
        Func<int> currentCount,
        Action<EventHandler<int>> addHandler,
        Action<EventHandler<int>> removeHandler)
    {
        _currentCount = currentCount;
        _addHandler = addHandler;
        _removeHandler = removeHandler;
    }

    public IDisposable Subscribe(IObserver<int> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        int current;
        lock (_gate)
        {
            if (!_attached)
            {
                // Attach before recording the observer: attaching the native listener can
                // throw on an unsupported platform, and a throw here must leave no observer
                // and no half-attached state behind.
                _addHandler(OnCountChanged);
                _attached = true;
            }

            current = _currentCount();
            _observers.Add(observer);
        }

        // Push the replayed value outside the lock so a re-entrant subscribe or dispose from
        // the handler cannot deadlock.
        observer.OnNext(current);
        return new Subscription(this, observer);
    }

    private void OnCountChanged(object? sender, int count)
    {
        IObserver<int>[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _observers];
        }

        foreach (var observer in snapshot)
        {
            observer.OnNext(count);
        }
    }

    private void Remove(IObserver<int> observer)
    {
        lock (_gate)
        {
            if (_observers.Remove(observer) && _observers.Count == 0 && _attached)
            {
                _removeHandler(OnCountChanged);
                _attached = false;
            }
        }
    }

    private sealed class Subscription(UnreadConversationCountObservable owner, IObserver<int> observer)
        : IDisposable
    {
        private UnreadConversationCountObservable? _owner = owner;

        // Interlocked so a double-dispose unsubscribes exactly once.
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Remove(observer);
    }
}
