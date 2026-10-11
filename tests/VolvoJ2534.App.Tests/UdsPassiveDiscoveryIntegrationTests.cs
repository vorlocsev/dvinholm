namespace VolvoJ2534.App.Tests;

public sealed class UdsPassiveDiscoveryIntegrationTests
{
    [Fact]
    public async Task ScanAsync_CollectsUdsResponsesWithoutTransmitting()
    {
        using var adapter = new VirtualJ2534Adapter(
            _ => Array.Empty<VolvoJ2534.App.CanFrame>());
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        var discovery = new VolvoJ2534.App.UdsEcuDiscovery(bus);
        var scan = discovery.ScanAsync(TimeSpan.FromMilliseconds(250));

        // Allow the asynchronous scan to register its receive subscription.
        await Task.Delay(50);
        adapter.InjectReceiveFrame(new VolvoJ2534.App.CanFrame(
            0x7E8,
            false,
            false,
            new byte[] { 0x03, 0x62, 0xF1, 0x90 },
            0,
            0));

        var candidates = await scan;

        var candidate = Assert.Single(candidates);
        Assert.Equal((uint)0x7E8, candidate.ResponseId);
        Assert.Equal((uint?)0x7E0, candidate.RequestId);
        Assert.False(candidate.IsExtended);
        Assert.Equal(1, candidate.ResponseCount);
        Assert.Equal(4, candidate.MaxDataLength);
        Assert.Equal(0, adapter.TransmittedFrames);
    }
    [Fact]
    public async Task ScanAsync_CancellationStopsPassiveScanPromptly()
    {
        using var adapter = new VirtualJ2534Adapter(
            _ => Array.Empty<VolvoJ2534.App.CanFrame>());
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        var discovery = new VolvoJ2534.App.UdsEcuDiscovery(bus);
        using var cancellation = new CancellationTokenSource();
        var scan = discovery.ScanAsync(TimeSpan.FromSeconds(5), cancellation.Token);

        await Task.Delay(50);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await scan.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal(0, adapter.TransmittedFrames);
    }

}
