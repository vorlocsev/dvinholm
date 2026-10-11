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
    public void IsLikelyUdsResponse_RejectsTruncatedNegativeResponse()
    {
        var truncated = new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x02, 0x7F, 0x22 }, 0, 0);

        Assert.False(Dvinholm.App.UdsEcuDiscovery.IsLikelyUdsResponse(truncated, out _));
    }

    [Fact]
    public void IsLikelyUdsResponse_AcceptsNegativeResponseWithNrc()
    {
        var negative = new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x03, 0x7F, 0x22, 0x31 }, 0, 0);

        Assert.True(Dvinholm.App.UdsEcuDiscovery.IsLikelyUdsResponse(negative, out var service));
        Assert.Equal((byte)0x7F, service);
    }

    [Fact]
    public void GuessRequestId_InfersOnlySupportedStandardAddressing()
    {
        Assert.Equal((uint?)0x7E0, Dvinholm.App.UdsEcuDiscovery.GuessRequestId(0x7E8, false));
        Assert.Equal((uint?)0x18DA01F1,
            Dvinholm.App.UdsEcuDiscovery.GuessRequestId(0x18DAF101, true));
        Assert.Null(Dvinholm.App.UdsEcuDiscovery.GuessRequestId(0x18DAF101, false));
        Assert.Null(Dvinholm.App.UdsEcuDiscovery.GuessRequestId(0x18DA1201, true));
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
