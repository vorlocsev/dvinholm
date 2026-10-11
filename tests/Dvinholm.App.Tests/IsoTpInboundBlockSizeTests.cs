namespace VolvoJ2534.App.Tests;

public sealed class IsoTpInboundBlockSizeTests
{
    [Fact]
    public void Request_SendsFlowControlAfterEachConsecutiveFrameWhenRxBlockSizeIsOne()
    {
        var flowControlFrames = 0;
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<VolvoJ2534.App.CanFrame>();

            if ((request.Data[0] >> 4) == 0)
            {
                // A 20-byte ISO-TP response: six bytes in FF and seven in each CF.
                return new[]
                {
                    new VolvoJ2534.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 },
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
                        new VolvoJ2534.App.CanFrame(
                            0x7E8, false, false,
                            new byte[] { 0x21, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A },
                            0, 0)
                    },
                    2 => new[]
                    {
                        new VolvoJ2534.App.CanFrame(
                            0x7E8, false, false,
                            new byte[] { 0x22, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11 },
                            0, 0)
                    },
                    _ => Array.Empty<VolvoJ2534.App.CanFrame>()
                };
            }

            return Array.Empty<VolvoJ2534.App.CanFrame>();
        });

        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                RxBlockSize: 1,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });

        Assert.Equal(
            new byte[] { 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11 },
            response);
        Assert.Equal(2, Volatile.Read(ref flowControlFrames));
        // One UDS request plus two tester Flow Control frames.
        Assert.Equal(3, adapter.TransmittedFrames);
    }
}
