namespace Dvinholm.App.Tests;

public sealed class CanRxSubscriberIsolationTests
{
    [Fact]
    public void Dispatcher_SlowMonitorOverflowDoesNotDropFramesForOtherSubscriber()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<Dvinholm.App.CanFrame>());
        using var dispatcher = new Dvinholm.App.CanRxDispatcher(adapter);
        using var monitor = dispatcher.Subscribe(capacity: 1);
        using var diagnostic = dispatcher.Subscribe(capacity: 8);
        dispatcher.Start();

        // Inject a burst before either subscriber reads. The monitor's bounded
        // queue can retain only the newest frame, while the diagnostic
        // subscriber has enough capacity to preserve the complete burst.
        for (uint id = 0x100; id <= 0x103; id++)
        {
            adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
                id, false, false, new byte[] { 0x01, (byte)id }, 0, 0));
        }

        var diagnosticIds = new List<uint>();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (diagnosticIds.Count < 4 && DateTime.UtcNow < deadline)
        {
            if (diagnostic.TryRead(TimeSpan.FromMilliseconds(100), CancellationToken.None, out var frame))
                diagnosticIds.Add(frame.ArbitrationId);
        }

        Assert.Equal(new uint[] { 0x100, 0x101, 0x102, 0x103 }, diagnosticIds);

        Assert.True(monitor.TryRead(TimeSpan.FromSeconds(1), CancellationToken.None, out var latest));
        Assert.Equal(0x103u, latest.ArbitrationId);
        Assert.False(monitor.TryRead(TimeSpan.Zero, CancellationToken.None, out _));
    }
}
