namespace Dvinholm.App.Tests;

public sealed class SharedCanMonitorUdsDispatcherTests
{
    [Fact]
    public void UdsRequestAndCanMonitorReceiveSameResponseThroughSharedDispatcher()
    {
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 ||
                request.IsExtended ||
                request.Data.Length < 4 ||
                request.Data[0] >> 4 != 0 ||
                request.Data[1] != 0x22 ||
                request.Data[2] != 0xF1 ||
                request.Data[3] != 0x91)
            {
                return Array.Empty<Dvinholm.App.CanFrame>();
            }

            return new[]
            {
                new Dvinholm.App.CanFrame(
                    0x7E8,
                    false,
                    false,
                    new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 },
                    0,
                    0)
            };
        });

        using var bus = new Dvinholm.App.CanBus(adapter);
        using var monitor = bus.Subscribe();
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                RequestId: 0x7E0,
                ResponseId: 0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));
        using var client = new Dvinholm.App.UdsClient(channel);

        var data = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42 }, data);
        Assert.True(
            monitor.TryRead(TimeSpan.FromSeconds(1), CancellationToken.None, out var observed),
            "The CAN monitor did not receive the ECU response seen by UDS.");
        Assert.Equal((uint)0x7E8, observed.ArbitrationId);
        Assert.Equal(new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 }, observed.Data);
        Assert.Equal(1, adapter.TransmittedFrames);
    }
}
