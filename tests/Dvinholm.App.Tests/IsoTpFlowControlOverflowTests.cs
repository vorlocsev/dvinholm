namespace VolvoJ2534.App.Tests;

public sealed class IsoTpFlowControlOverflowTests
{
    [Fact]
    public void Request_AbortsWhenReceiverReportsFlowControlOverflow()
    {
        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<VolvoJ2534.App.CanFrame>();

            // Flow Status 0x2 means Overflow: the receiver cannot accept the message.
            if ((request.Data[0] >> 4) == 1)
            {
                return new[]
                {
                    new VolvoJ2534.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x32, 0x00, 0x00, 0, 0, 0, 0, 0 },
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

        var request = Enumerable.Range(0, 27).Select(i => (byte)i).ToArray();
        var error = Assert.Throws<InvalidOperationException>(() => channel.Request(request));

        Assert.Contains("Overflow", error.Message, StringComparison.OrdinalIgnoreCase);
        // The sender must stop after the First Frame; no Consecutive Frames may follow.
        Assert.Equal(1, adapter.TransmittedFrames);
    }
}
