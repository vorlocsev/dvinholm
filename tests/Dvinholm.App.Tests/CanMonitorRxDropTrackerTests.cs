namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, UI-thread-style RX drop-accounting checks. No physical
/// adapter, CAN payload, CEM service, PIN or write operation.
/// </summary>
public sealed class CanMonitorRxDropTrackerTests
{
    [Fact]
    public void NoSubscriptionSinceClearIsUnknown()
    {
        var tracker = new CanMonitorRxDropTracker();
        Assert.Null(tracker.Snapshot(null));
        Assert.Null(tracker.Snapshot(0));
    }

    [Fact]
    public void ActiveSubscriptionReportsOnlyDropsSinceItsStart()
    {
        var tracker = new CanMonitorRxDropTracker();
        tracker.Start(4);
        Assert.Equal(0, tracker.Snapshot(4));
        Assert.Equal(3, tracker.Snapshot(7));
    }

    [Fact]
    public void CompletedSubscriptionRetainsFinalDropCount()
    {
        var tracker = new CanMonitorRxDropTracker();
        tracker.Start(0);
        tracker.Complete(12);
        Assert.Equal(12, tracker.Snapshot(null));
    }

    [Fact]
    public void RestartAddsDropsWithoutResettingEarlierHistory()
    {
        var tracker = new CanMonitorRxDropTracker();
        tracker.Start(0);
        tracker.Complete(3);
        tracker.Start(0);
        Assert.Equal(3, tracker.Snapshot(0));
        tracker.Complete(5);
        Assert.Equal(8, tracker.Snapshot(null));
    }

    [Fact]
    public void ClearWhileRunningRebasesCurrentSubscription()
    {
        var tracker = new CanMonitorRxDropTracker();
        tracker.Start(0);
        Assert.Equal(6, tracker.Snapshot(6));
        tracker.Clear(6);
        Assert.Equal(0, tracker.Snapshot(6));
        tracker.Complete(10);
        Assert.Equal(4, tracker.Snapshot(null));
    }

    [Fact]
    public void ClearAfterStopForgetsPreviousEvidenceAndDrops()
    {
        var tracker = new CanMonitorRxDropTracker();
        tracker.Start(0);
        tracker.Complete(9);
        tracker.Clear(null);
        Assert.Null(tracker.Snapshot(null));
        tracker.Start(0);
        Assert.Equal(0, tracker.Snapshot(0));
    }

    [Fact]
    public void MissingOrRegressingActiveCounterFailsClosed()
    {
        var tracker = new CanMonitorRxDropTracker();
        tracker.Start(0);
        Assert.Equal(3, tracker.Snapshot(3));
        Assert.Null(tracker.Snapshot(null));
        Assert.Null(tracker.Snapshot(2));
        Assert.Throws<InvalidDataException>(() => tracker.Complete(2));
        Assert.Throws<InvalidDataException>(() => tracker.Clear(null));
        Assert.Equal(3, tracker.Snapshot(3));
    }

    [Fact]
    public void InvalidTransitionsAndOverflowNeverProduceWrappedCounters()
    {
        var tracker = new CanMonitorRxDropTracker();
        Assert.Throws<InvalidDataException>(() => tracker.Start(-1));
        Assert.Throws<InvalidDataException>(() => tracker.Complete(0));
        tracker.Start(0);
        Assert.Throws<InvalidDataException>(() => tracker.Start(0));
        tracker.Complete(long.MaxValue);
        tracker.Start(0);
        Assert.Null(tracker.Snapshot(1));
        Assert.Throws<InvalidDataException>(() => tracker.Complete(1));
    }
}
