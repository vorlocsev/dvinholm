namespace Dvinholm.App.Tests;

public sealed class IsoTpMalformedFlowControlRecoveryTests
{
    [Fact]
    public void Request_RejectsTruncatedFlowControlAndAllowsNextTransaction()
    {
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<Dvinholm.App.CanFrame>();

            var frameType = request.Data[0] >> 4;
            if (frameType == 1)
            {
                // Flow Control requires Flow Status, Block Size, and STmin.
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x30, 0x00 },
                        0, 0)
                };
            }

            if (frameType == 0)
            {
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x03, 0x62, 0xF1, 0x91 },
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
                RequestTimeout: TimeSpan.FromSeconds(1)));

        var longRequest = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        var error = Assert.Throws<InvalidOperationException>(() => channel.Request(longRequest));

        Assert.Contains("Flow Control", error.Message, StringComparison.OrdinalIgnoreCase);
        // The malformed FC must stop the multi-frame send before any CF is sent.
        Assert.Equal(1, adapter.TransmittedFrames);

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });
        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.TransmittedFrames);
    }
}
