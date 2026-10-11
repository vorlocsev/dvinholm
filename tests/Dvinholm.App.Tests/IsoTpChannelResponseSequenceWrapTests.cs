namespace VolvoJ2534.App.Tests;

public sealed class IsoTpChannelResponseSequenceWrapTests
{
    [Fact]
    public void Request_ReassemblesMultiFrameResponseAcrossSequenceNumberWrap()
    {
        var expected = Enumerable.Range(0, 120).Select(i => (byte)(i & 0xFF)).ToArray();
        var responseFrames = VolvoJ2534.App.IsoTp.Segment(expected);

        using var adapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId != 0x7E0 || request.Data.Length == 0)
                return Array.Empty<VolvoJ2534.App.CanFrame>();

            if ((request.Data[0] >> 4) == 0)
            {
                return new[]
                {
                    new VolvoJ2534.App.CanFrame(
                        0x7E8, false, false, responseFrames[0], 0, 0)
                };
            }

            if ((request.Data[0] >> 4) == 3)
            {
                // BS=0 permits the ECU to transmit the remaining CFs as one block.
                return responseFrames.Skip(1)
                    .Select(bytes => new VolvoJ2534.App.CanFrame(
                        0x7E8, false, false, bytes, 0, 0))
                    .ToArray();
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
                RxBlockSize: 0,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var actual = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });

        Assert.Equal(18, responseFrames.Count); // First Frame plus 17 Consecutive Frames.
        Assert.Equal(0x2F, responseFrames[15][0]);
        Assert.Equal(0x20, responseFrames[16][0]);
        Assert.Equal(expected, actual);
        Assert.Equal(2, adapter.TransmittedFrames); // Request plus one initial Flow Control.
    }
}
