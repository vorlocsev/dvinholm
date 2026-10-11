namespace Dvinholm.App.Tests;

public sealed class UdsReadDtcIntegrationTests
{
    [Fact]
    public void ReadDtcByStatusMask_SendsCorrectRequestAndParsesResponse()
    {
        byte[]? transmittedRequest = null;
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<Dvinholm.App.CanFrame>();

            if ((request.Data[0] >> 4) == 0)
            {
                Volatile.Write(ref transmittedRequest, request.Data.ToArray());
                if (request.Data.Length > 1 && request.Data[1] == 0x19)
                {
                    return new[]
                    {
                        new Dvinholm.App.CanFrame(
                            0x7E8, false, false,
                            new byte[] { 0x07, 0x59, 0x02, 0xFF, 0x12, 0x34, 0x56, 0x2A },
                            0, 0)
                    };
                }
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
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));
        using var client = new Dvinholm.App.UdsClient(channel);

        var records = client.ReadDtcByStatusMask(0xFF);

        Assert.Equal(new byte[] { 0x03, 0x19, 0x02, 0xFF },
            Volatile.Read(ref transmittedRequest)!.Take(4));
        var record = Assert.Single(records);
        Assert.Equal((uint)0x123456, record.Code);
        Assert.Equal((byte)0x2A, record.Status);
        Assert.Equal(1, adapter.TransmittedFrames);
    }
}
