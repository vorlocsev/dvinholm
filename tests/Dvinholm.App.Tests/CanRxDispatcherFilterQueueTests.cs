namespace Dvinholm.App.Tests;

public sealed class CanRxDispatcherFilterQueueTests
{
    [Fact]
    public void Subscription_FilterRunsBeforeQueueCapacityAndDoesNotDropMatchingFrames()
    {
        using var j2534 = new Dvinholm.App.J2534Native();
        using var dispatcher = new Dvinholm.App.CanRxDispatcher(j2534);
        using var diagnostic = dispatcher.Subscribe(
            capacity: 1,
            filter: frame => frame.ArbitrationId == 0x7E8);

        // Unrelated traffic must not consume the diagnostic queue's only slot,
        // even when the bus is otherwise busy.
        for (uint id = 0x100; id < 0x120; id++)
            diagnostic.Publish(Frame(id));

        var expected = Frame(0x7E8);
        diagnostic.Publish(expected);

        Assert.Equal(0, diagnostic.DroppedFrameCount);
        Assert.True(
            diagnostic.TryRead(TimeSpan.Zero, CancellationToken.None, out var actual),
            "Matching diagnostic frame was not queued.");
        Assert.Equal(expected.ArbitrationId, actual.ArbitrationId);
        Assert.Equal(expected.Data, actual.Data);
        Assert.False(diagnostic.TryRead(TimeSpan.Zero, CancellationToken.None, out _));
    }

    private static Dvinholm.App.CanFrame Frame(uint id) =>
        new(id, false, false, new byte[] { 0x03, 0x7F, 0x22, 0x78 }, 0, 0);
}
