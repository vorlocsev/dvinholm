namespace Dvinholm.App.Tests;

public sealed class IsoTpInboundFlowControlParametersTests
{
    [Fact]
    public void Request_AdvertisesConfiguredBlockSizeAndSeparationTime()
    {
        byte[]? transmittedFlowControl = null;
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
                        new byte[] { 0x10, 0x0C, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 },
                        0, 0)
                };
            }

            if ((request.Data[0] >> 4) == 3)
            {
                Volatile.Write(ref transmittedFlowControl, request.Data.ToArray());
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x21, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x00 },
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
                RxBlockSize: 2,
                RxSeparationTime: 0x05,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });

        Assert.Equal(
            new byte[] { 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 },
            response);
        Assert.Equal(
            new byte[] { 0x30, 0x02, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00 },
            Volatile.Read(ref transmittedFlowControl));
        Assert.Equal(2, adapter.TransmittedFrames);
    }
}
