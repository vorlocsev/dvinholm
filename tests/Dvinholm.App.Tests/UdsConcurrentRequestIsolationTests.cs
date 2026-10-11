namespace Dvinholm.App.Tests;

public sealed class UdsConcurrentRequestIsolationTests
{
    [Fact]
    public async Task ConcurrentReadDataByIdentifierRequests_ReturnMatchingResponses()
    {
        using var adapter = new VirtualJ2534Adapter(frame =>
        {
            // Only respond to single-frame UDS ReadDataByIdentifier requests.
            if (frame.Data.Length < 4 || (frame.Data[0] >> 4) != 0 ||
                frame.Data[1] != 0x22)
                return Array.Empty<Dvinholm.App.CanFrame>();

            var didHigh = frame.Data[2];
            var didLow = frame.Data[3];
            return new[]
            {
                new Dvinholm.App.CanFrame(
                    0x7E8,
                    false,
                    false,
                    new byte[] { 0x04, 0x62, didHigh, didLow, (byte)(didLow ^ 0x5A) },
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
                FrameTimeout: TimeSpan.FromSeconds(1),
                RequestTimeout: TimeSpan.FromSeconds(3)));
        using var client = new Dvinholm.App.UdsClient(channel);

        var first = Task.Run(() => client.ReadDataByIdentifier(0xF190));
        var second = Task.Run(() => client.ReadDataByIdentifier(0xF191));

        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(4));

        Assert.Equal(new byte[] { 0xF1, 0x90, (byte)(0x90 ^ 0x5A) }, results[0]);
        Assert.Equal(new byte[] { 0xF1, 0x91, (byte)(0x91 ^ 0x5A) }, results[1]);
        Assert.Equal(2, adapter.TransmittedFrames);
    }
}
