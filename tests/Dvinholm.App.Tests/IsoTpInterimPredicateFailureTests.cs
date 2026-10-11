namespace Dvinholm.App.Tests;

public sealed class IsoTpInterimPredicateFailureTests
{
    [Fact]
    public void Request_InterimResponsePredicateThrows_ChannelCanBeUsedAgain()
    {
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 ||
                request.Data.Length < 4 ||
                request.Data[1] != 0x22 ||
                request.Data[2] != 0xF1)
            {
                return Array.Empty<Dvinholm.App.CanFrame>();
            }

            return new[]
            {
                new Dvinholm.App.CanFrame(
                    0x7E8,
                    false,
                    false,
                    new byte[] { 0x04, 0x62, request.Data[2], request.Data[3], 0x42 },
                    0,
                    0)
            };
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

        var error = Assert.Throws<InvalidOperationException>(() =>
            channel.Request(
                new byte[] { 0x22, 0xF1, 0x90 },
                _ => throw new InvalidOperationException("Test predicate failure.")));

        Assert.Equal("Test predicate failure.", error.Message);

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91, 0x42 }, response);
        Assert.Equal(2, adapter.TransmittedFrames);
    }
}
