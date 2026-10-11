namespace Dvinholm.App.Tests;

public sealed class IsoTpInboundBlockSizeTwoTests
{
    [Fact]
    public void Request_SendsFlowControlAfterEachBlockOfTwoConsecutiveFrames()
    {
        var flowControlFrames = 0;
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<Dvinholm.App.CanFrame>();

            if ((request.Data[0] >> 4) == 0)
            {
                // 27-byte response: six bytes in the First Frame and three CFs.
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x10, 0x1B, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 },
                        0, 0)
                };
            }

            if ((request.Data[0] >> 4) == 3)
            {
                var number = Interlocked.Increment(ref flowControlFrames);
                return number switch
                {
                    1 => new[]
                    {
                        new Dvinholm.App.CanFrame(
                            0x7E8, false, false,
                            new byte[] { 0x21, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A },
                            0, 0),
                        new Dvinholm.App.CanFrame(
                            0x7E8, false, false,
                            new byte[] { 0x22, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11 },
                            0, 0)
                    },
                    2 => new[]
                    {
                        new Dvinholm.App.CanFrame(
                            0x7E8, false, false,
                            new byte[] { 0x23, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18 },
                            0, 0)
                    },
                    _ => Array.Empty<Dvinholm.App.CanFrame>()
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
                RxBlockSize: 2,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });

        Assert.Equal(
            new byte[] { 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18 },
            response);
        Assert.Equal(2, Volatile.Read(ref flowControlFrames));
        // One request and two Flow Control frames: initial and after CF #2.
        Assert.Equal(3, adapter.TransmittedFrames);
    }
}
