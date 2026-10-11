namespace Dvinholm.App.Tests;

public sealed class UdsEcuDiscoveryScanTests
{
    [Fact]
    public async Task ScanAsync_CollectsOnlyLikelyUdsResponsesWithoutTransmitting()
    {
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();
        var discovery = new Dvinholm.App.UdsEcuDiscovery(bus);

        var scan = discovery.ScanAsync(TimeSpan.FromMilliseconds(400));
        // Allow the scan task to establish its subscription before injecting
        // frames into the virtual adapter's receive queue.
        await Task.Delay(50);

        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x90 }, 0, 0));
        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x90 }, 0, 0));
        // Request SID and continuation frame must not be reported as ECUs.
        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E0, false, false, new byte[] { 0x03, 0x22, 0xF1, 0x90 }, 0, 0));
        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E9, false, false, new byte[] { 0x21, 0x62, 0xF1, 0x90 }, 0, 0));

        var candidates = await scan.WaitAsync(TimeSpan.FromSeconds(2));

        var candidate = Assert.Single(candidates);
        Assert.Equal((uint)0x7E8, candidate.ResponseId);
        Assert.Equal((uint?)0x7E0, candidate.RequestId);
        Assert.False(candidate.IsExtended);
        Assert.Equal(2, candidate.ResponseCount);
        Assert.Equal(4, candidate.MaxDataLength);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Fact]
    public async Task ScanAsync_ThrowsWhenCancelled()
    {
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();
        var discovery = new Dvinholm.App.UdsEcuDiscovery(bus);
        using var cancellation = new CancellationTokenSource();

        var scan = discovery.ScanAsync(TimeSpan.FromSeconds(5), cancellation.Token);
        await Task.Delay(50);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await scan.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(0, adapter.TransmittedFrames);
    }
}
