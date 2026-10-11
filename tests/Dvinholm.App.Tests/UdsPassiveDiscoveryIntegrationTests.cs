namespace Dvinholm.App.Tests;

public sealed class UdsPassiveDiscoveryIntegrationTests
{
    [Fact]
    public async Task ScanAsync_CollectsUdsResponsesWithoutTransmitting()
    {
        using var adapter = new VirtualJ2534Adapter(
            _ => Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        var discovery = new Dvinholm.App.UdsEcuDiscovery(bus);
        var scan = discovery.ScanAsync(TimeSpan.FromMilliseconds(250));

        // Allow the asynchronous scan to register its receive subscription.
        await Task.Delay(50);
        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
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
}
