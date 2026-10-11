namespace Dvinholm.App.Tests;

public sealed class IsoTpStminRequestDeadlineTests
{
    [Fact]
    public void Request_RejectsSeparationTimeBeyondRemainingDeadlineAndRecovers()
    {
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<Dvinholm.App.CanFrame>();

            var frameType = request.Data[0] >> 4;
            if (frameType == 1)
            {
                // 127 ms STmin cannot fit inside the request's 100 ms deadline.
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x30, 0x00, 0x7F, 0, 0, 0, 0, 0 },
                        0, 0)
                };
            }

            if (frameType == 0)
            {
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 },
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
                FlowControlTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromMilliseconds(100)));

        var longRequest = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        var error = Assert.Throws<TimeoutException>(() => channel.Request(longRequest));

        Assert.Contains("STmin exceeds remaining request timeout", error.Message, StringComparison.OrdinalIgnoreCase);
        // The First Frame was sent, but no Consecutive Frame may be sent when
        // its mandatory delay cannot fit inside the absolute request deadline.
        Assert.Equal(1, adapter.TransmittedFrames);

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });
        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91, 0x42 }, response);
        Assert.Equal(2, adapter.TransmittedFrames);
    }
}
