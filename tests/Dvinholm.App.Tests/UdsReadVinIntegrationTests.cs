namespace Dvinholm.App.Tests;

public sealed class UdsReadVinIntegrationTests
{
    [Fact]
    public void ReadVin_RequestsVinDidAndReassemblesMultiFrameResponse()
    {
        byte[]? transmittedRequest = null;
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<Dvinholm.App.CanFrame>();

            if ((request.Data[0] >> 4) == 0)
            {
                Volatile.Write(ref transmittedRequest, request.Data.ToArray());
                if (request.Data.Length > 2 && request.Data[1] == 0x22 &&
                    request.Data[2] == 0xF1 && request.Data[3] == 0x90)
                {
                    return new[]
                    {
                        new Dvinholm.App.CanFrame(
                            0x7E8, false, false,
                            new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, 0x59, 0x56, 0x31 },
                            0, 0)
                    };
                }
            }

            if ((request.Data[0] >> 4) == 3)
            {
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x21, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x31 },
                        0, 0),
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x22, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38 },
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
        using var client = new Dvinholm.App.UdsClient(channel);

        var vin = client.ReadVin();

        Assert.Equal(new byte[] { 0x03, 0x22, 0xF1, 0x90 },
            Volatile.Read(ref transmittedRequest)!.Take(4));
        Assert.Equal("YV1ABCDEF12345678", vin);
        Assert.Equal(2, adapter.TransmittedFrames);
    }
}
