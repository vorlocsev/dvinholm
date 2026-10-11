namespace Dvinholm.App.Tests;

public sealed class IsoTpInboundUnlimitedBlockSizeTests
{
    [Fact]
    public void Request_SendsOnlyInitialFlowControlWhenRxBlockSizeIsZero()
    {
        var flowControlFrames = 0;
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<Dvinholm.App.CanFrame>();

            if ((request.Data[0] >> 4) == 0)
            {
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 },
                        0, 0)
                };
            }

            if ((request.Data[0] >> 4) == 3)
            {
                Interlocked.Increment(ref flowControlFrames);
                // BS=0 means the sender may transmit all remaining CFs without
                // waiting for another tester Flow Control frame.
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x21, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A },
                        0, 0),
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x22, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11 },
                        0, 0)
                };
            }

            return Array.Empty<Dvinholm.App.CanFrame>();
        });

        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                RxBlockSize: 0,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });

        Assert.Equal(
            new byte[] { 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11 },
            response);
        Assert.Equal(1, Volatile.Read(ref flowControlFrames));
        // One UDS request and exactly one tester Flow Control frame.
        Assert.Equal(2, adapter.TransmittedFrames);
    }
}
