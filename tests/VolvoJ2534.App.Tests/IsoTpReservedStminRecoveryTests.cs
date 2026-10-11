namespace VolvoJ2534.App.Tests;

public sealed class IsoTpReservedStminRecoveryTests
{
    [Fact]
    public void Request_RejectsReservedFlowControlStminAndAllowsNextTransaction()
    {
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<VolvoJ2534.App.CanFrame>();

            var frameType = request.Data[0] >> 4;
            if (frameType == 1)
            {
                // 0x80 is reserved by ISO-TP and must not be interpreted as a delay.
                return new[]
                {
                    new VolvoJ2534.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x30, 0x00, 0x80, 0, 0, 0, 0, 0 },
                        0, 0)
                };
            }

            if (frameType == 0)
            {
                return new[]
                {
                    new VolvoJ2534.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x03, 0x62, 0xF1, 0x91 },
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
                0x7E0,
                0x7E8,
                FlowControlTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(1)));

        var longRequest = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        var error = Assert.Throws<InvalidOperationException>(() => channel.Request(longRequest));

        Assert.Contains("Reserved ISO-TP STmin", error.Message, StringComparison.OrdinalIgnoreCase);
        // Only the First Frame may be transmitted before the invalid FC is rejected.
        Assert.Equal(1, adapter.TransmittedFrames);

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });
        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.TransmittedFrames);
    }
}
