namespace Dvinholm.App.Tests;

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
                return Array.Empty<Dvinholm.App.CanFrame>();
            }

            return new[]
            {
                new Dvinholm.App.CanFrame(
                    0x18DAF110,
                    true,
                    false,
                    new byte[] { 0x03, 0x62, 0xF1, 0x90, 0, 0, 0, 0 },
                    0,
                    0)
            };
        });

        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
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
