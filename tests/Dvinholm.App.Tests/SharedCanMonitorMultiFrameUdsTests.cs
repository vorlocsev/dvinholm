namespace Dvinholm.App.Tests;

public sealed class SharedCanMonitorMultiFrameUdsTests
{
    [Fact]
    public void MonitorObservesAllIsoTpFramesWhileUdsReassemblesVin()
    {
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.IsExtended)
                return Array.Empty<Dvinholm.App.CanFrame>();

            if (request.Data.Length >= 4 &&
                request.Data[0] >> 4 == 0 &&
                request.Data[1] == 0x22 &&
                request.Data[2] == 0xF1 &&
                request.Data[3] == 0x90)
            {
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, (byte)'Y', (byte)'V', (byte)'1' },
                        0, 0)
                };
            }

            if (request.Data.Length >= 3 && request.Data[0] >> 4 == 3)
            {
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x21, (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', (byte)'1' },
                        0, 0),
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x22, (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8' },
                        0, 0)
                };
            }

            return Array.Empty<Dvinholm.App.CanFrame>();
        });

        using var bus = new Dvinholm.App.CanBus(adapter);
        using var monitor = bus.Subscribe();
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                RequestId: 0x7E0,
                ResponseId: 0x7E8,
                FrameTimeout: TimeSpan.FromSeconds(1),
                ConsecutiveFrameTimeout: TimeSpan.FromSeconds(1),
                RequestTimeout: TimeSpan.FromSeconds(3)));
        using var client = new Dvinholm.App.UdsClient(channel);

        Assert.Equal("YV1ABCDEF12345678", client.ReadVin());

        var observed = new List<Dvinholm.App.CanFrame>();
        for (var i = 0; i < 3; i++)
        {
            Assert.True(
                monitor.TryRead(TimeSpan.FromSeconds(1), CancellationToken.None, out var frame),
                $"CAN monitor missed response frame {i + 1}.");
            observed.Add(frame);
        }

        Assert.Equal(new uint[] { 0x7E8, 0x7E8, 0x7E8 },
            observed.Select(frame => frame.ArbitrationId));
        Assert.Equal(new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, (byte)'Y', (byte)'V', (byte)'1' }, observed[0].Data);
        Assert.Equal(new byte[] { 0x21, (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', (byte)'1' }, observed[1].Data);
        Assert.Equal(new byte[] { 0x22, (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8' }, observed[2].Data);
        Assert.Equal(2, adapter.TransmittedFrames);
    }
}
