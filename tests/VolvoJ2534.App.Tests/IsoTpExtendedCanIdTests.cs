namespace VolvoJ2534.App.Tests;

public sealed class IsoTpExtendedCanIdTests
{
    [Fact]
    public void Request_UsesConfigured29BitCanIdentifiers()
    {
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x18DA10F1 ||
                !request.IsExtended ||
                request.Data.Length == 0 ||
                (request.Data[0] >> 4) != 0)
            {
                return Array.Empty<VolvoJ2534.App.CanFrame>();
            }

            return new[]
            {
                new VolvoJ2534.App.CanFrame(
                    0x18DAF110,
                    false,
                    true,
                    new byte[] { 0x03, 0x62, 0xF1, 0x90, 0, 0, 0, 0 },
                    0,
                    0)
            };
        });

        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x18DA10F1,
                0x18DAF110,
                CanExtendedId: true,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x90 }, response);
        Assert.Equal(1, adapter.TransmittedFrames);
    }
}
