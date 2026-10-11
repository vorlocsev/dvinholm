namespace VolvoJ2534.App.Tests;

public sealed class UdsExtendedCanIdIntegrationTests
{
    [Fact]
    public void ReadDataByIdentifier_WorksWithExtendedCanIds()
    {
        const uint requestId = 0x18DA10F1;
        const uint responseId = 0x18DAF110;
        byte[]? transmittedRequest = null;

        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != requestId || !request.IsExtended || request.Data.Length == 0)
                return Array.Empty<VolvoJ2534.App.CanFrame>();

            if ((request.Data[0] >> 4) == 0 &&
                request.Data.Length >= 4 &&
                request.Data[1] == 0x22 &&
                request.Data[2] == 0xF1 &&
                request.Data[3] == 0x90)
            {
                Volatile.Write(ref transmittedRequest, request.Data.ToArray());
                return new[]
                {
                    new VolvoJ2534.App.CanFrame(
                        responseId, true, false,
                        new byte[] { 0x05, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x00, 0x00 },
                        0, 0)
                };
            }

            return Array.Empty<VolvoJ2534.App.CanFrame>();
        });

        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                requestId,
                responseId,
                CanExtendedId: true,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));
        using var client = new VolvoJ2534.App.UdsClient(channel);

        var data = client.ReadDataByIdentifier(0xF190);

        Assert.Equal(new byte[] { 0x03, 0x22, 0xF1, 0x90 },
            Volatile.Read(ref transmittedRequest)!.Take(4));
        Assert.Equal(new byte[] { 0xF1, 0x90, 0x01, 0x02 }, data);
        Assert.Equal(1, adapter.TransmittedFrames);
    }
}
