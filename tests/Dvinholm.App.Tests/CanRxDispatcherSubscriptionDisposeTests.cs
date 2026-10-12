namespace Dvinholm.App.Tests;

public sealed class CanRxDispatcherSubscriptionDisposeTests
{
    [Fact]
    public async Task Subscription_DisposeWakesAllBlockedReaders()
    {
        using var j2534 = new Dvinholm.App.J2534Native();
        using var dispatcher = new Dvinholm.App.CanRxDispatcher(j2534);
        var subscription = dispatcher.Subscribe();
        using var readersStarted = new CountdownEvent(3);

        var readers = Enumerable.Range(0, 3).Select(index => Task.Run(() =>
        {
            readersStarted.Signal();
            return subscription.TryRead(
                TimeSpan.FromSeconds(10),
                CancellationToken.None,
                out var frame);
        })).ToArray();

        Assert.True(readersStarted.Wait(TimeSpan.FromSeconds(2)),
            "Readers did not start.");

        // Give each reader an opportunity to enter its channel wait. Disposal
        // completes the channel and must release every waiter without waiting
        // for its original timeout.
        await Task.Delay(50);
        subscription.Dispose();

        var results = await Task.WhenAll(readers).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.All(results, result => Assert.False(result));
        subscription.Publish(new Dvinholm.App.CanFrame(
            0x100, false, false, new byte[] { 0x01 }, 0, 0));
        Assert.False(subscription.TryRead(TimeSpan.Zero, CancellationToken.None, out _));
    }
}
