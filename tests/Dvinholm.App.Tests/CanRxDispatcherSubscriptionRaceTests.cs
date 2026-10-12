namespace Dvinholm.App.Tests;

public sealed class CanRxDispatcherSubscriptionRaceTests
{
    [Fact]
    public async Task ConcurrentPublishAndDisposeDoesNotThrowOrExposeFramesAfterDisposal()
    {
        using var j2534 = new Dvinholm.App.J2534Native();
        using var dispatcher = new Dvinholm.App.CanRxDispatcher(j2534);
        var subscription = dispatcher.Subscribe(capacity: 8);
        using var start = new ManualResetEventSlim();

        var frame = new Dvinholm.App.CanFrame(
            0x100, false, false, new byte[] { 0x01 }, 0, 0);

        var publisher = Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < 10_000; i++)
                subscription.Publish(frame);
        });

        var disposer = Task.Run(() =>
        {
            start.Wait();
            subscription.Dispose();
        });

        start.Set();
        await Task.WhenAll(publisher, disposer).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(subscription.TryRead(
            TimeSpan.Zero, CancellationToken.None, out _));

        // Disposal is idempotent and must remain safe after racing publication.
        subscription.Dispose();
        subscription.Publish(frame);
        Assert.False(subscription.TryRead(
            TimeSpan.Zero, CancellationToken.None, out _));
    }
}
