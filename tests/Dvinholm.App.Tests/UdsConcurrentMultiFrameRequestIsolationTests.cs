namespace Dvinholm.App.Tests;

public sealed class UdsConcurrentMultiFrameRequestIsolationTests
{
    [Fact]
    public async Task ConcurrentReadDataByIdentifierRequests_KeepMultiFrameResponsesIsolated()
    {
        byte[]? currentConsecutiveFrame = null;
        using var adapter = new VirtualJ2534Adapter(frame =>
        {
            if (frame.ArbitrationId != 0x7E0 || frame.Data.Length == 0)
                return Array.Empty<Dvinholm.App.CanFrame>();

            var frameType = frame.Data[0] >> 4;
            if (frameType == 0 && frame.Data.Length >= 4 && frame.Data[1] == 0x22)
            {
                // Each DID produces a distinct 12-byte UDS payload, forcing a
                // First Frame, receiver Flow Control, and a Consecutive Frame.
                var didHigh = frame.Data[2];
                var didLow = frame.Data[3];
                currentConsecutiveFrame = new byte[]
                {
                    0x21, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0x00
                };

                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x10, 0x0C, 0x62, didHigh, didLow, 0xA0, 0xA1, 0xA2 },
                        0, 0)
                };
            }

            if (frameType == 3 && currentConsecutiveFrame is not null)
            {
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false, currentConsecutiveFrame, 0, 0)
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
                FrameTimeout: TimeSpan.FromSeconds(1),
                ConsecutiveFrameTimeout: TimeSpan.FromSeconds(1),
                RequestTimeout: TimeSpan.FromSeconds(4)));
        using var client = new Dvinholm.App.UdsClient(channel);

        var first = Task.Run(() => client.ReadDataByIdentifier(0xF190));
        var second = Task.Run(() => client.ReadDataByIdentifier(0xF191));

        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(
            new byte[] { 0xF1, 0x90, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8 },
            results[0]);
        Assert.Equal(
            new byte[] { 0xF1, 0x91, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8 },
            results[1]);
        Assert.Equal(4, adapter.TransmittedFrames); // Two UDS requests and two Flow Control frames.
    }
}
