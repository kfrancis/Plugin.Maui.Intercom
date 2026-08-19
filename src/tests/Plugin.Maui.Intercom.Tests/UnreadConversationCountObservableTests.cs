using CsCheck;

namespace Plugin.Maui.Intercom.Tests;

/// <summary>
///     The hand-rolled observable behind <see cref="IIntercom.UnreadConversationCounts" />:
///     replays the current value on subscribe, forwards changes, and holds the native listener
///     only while at least one observer is subscribed.
/// </summary>
public sealed class UnreadConversationCountObservableTests
{
    [Test]
    public async Task ReplaysCurrentValueOnSubscribe()
    {
        var (observable, _) = Build(7);
        var received = new List<int>();

        using var subscription = observable.Subscribe(new DelegateObserver(received.Add));

        await Assert.That(received).IsEquivalentTo([7]);
    }

    [Test]
    public async Task PushesSubsequentChanges()
    {
        var (observable, raise) = Build(0);
        var received = new List<int>();

        using var subscription = observable.Subscribe(new DelegateObserver(received.Add));
        raise(3);
        raise(5);

        await Assert.That(received).IsEquivalentTo([0, 3, 5]);
    }

    [Test]
    public async Task AttachesOnlyWhileSubscribed()
    {
        var attached = 0;
        var observable = new UnreadConversationCountObservable(
            () => 0,
            _ => attached++,
            _ => attached--);

        await Assert.That(attached).IsEqualTo(0);

        var subscription = observable.Subscribe(new DelegateObserver(_ => { }));
        await Assert.That(attached).IsEqualTo(1);

        subscription.Dispose();
        await Assert.That(attached).IsEqualTo(0);
    }

    [Test]
    public async Task AttachesOnceForMultipleObservers()
    {
        var attached = 0;
        var observable = new UnreadConversationCountObservable(
            () => 0,
            _ => attached++,
            _ => attached--);

        var first = observable.Subscribe(new DelegateObserver(_ => { }));
        var second = observable.Subscribe(new DelegateObserver(_ => { }));
        await Assert.That(attached).IsEqualTo(1);

        first.Dispose();
        // Still one observer left, so the native listener stays attached.
        await Assert.That(attached).IsEqualTo(1);

        second.Dispose();
        await Assert.That(attached).IsEqualTo(0);
    }

    [Test]
    public async Task DoubleDisposeUnsubscribesOnce()
    {
        var (observable, _) = Build(0);
        var received = new List<int>();
        var subscription = observable.Subscribe(new DelegateObserver(received.Add));

        subscription.Dispose();
        subscription.Dispose();

        await Assert.That(received).IsEquivalentTo([0]);
    }

    [Test]
    public void SurvivesConcurrentSubscribeAndDispose()
    {
        // Many threads churning subscribe/dispose must leave the native listener detached and
        // must never have it attached more than once: the observable's lock is what guarantees
        // the 0->1 and 1->0 transitions are the only ones that touch it.
        Gen.Int[2, 32].Sample(workers =>
        {
            var attached = 0;
            var maxAttached = 0;
            var maxGate = new Lock();
            var observable = new UnreadConversationCountObservable(
                () => 0,
                _ =>
                {
                    var value = Interlocked.Increment(ref attached);
                    lock (maxGate)
                    {
                        maxAttached = Math.Max(maxAttached, value);
                    }
                },
                _ => Interlocked.Decrement(ref attached));

            Parallel.For(0, workers, _ =>
            {
                for (var i = 0; i < 50; i++)
                {
                    var subscription = observable.Subscribe(new DelegateObserver(_ => { }));
                    subscription.Dispose();
                }
            });

            return Volatile.Read(ref attached) == 0 && maxAttached <= 1;
        });
    }

    // Returns the observable plus a delegate that raises a count change through whatever
    // handler is currently attached.
    private static (UnreadConversationCountObservable Observable, Action<int> Raise) Build(int currentCount)
    {
        EventHandler<int>? handler = null;
        var observable = new UnreadConversationCountObservable(
            () => currentCount,
            h => handler += h,
            h => handler -= h);
        return (observable, count => handler?.Invoke(null, count));
    }

    private sealed class DelegateObserver(Action<int> onNext) : IObserver<int>
    {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(int value)
        {
            onNext(value);
        }
    }
}
