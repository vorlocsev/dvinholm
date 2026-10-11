namespace Dvinholm.App.Tests;

public sealed class IsoTpSequenceMismatchRecoveryTests
{
    [Fact]
    public void Request_RejectsWrongConsecutiveFrameSequenceAndAllowsNextTransaction()
    {
        var requestCount = 0;
        using var adapter = new VirtualJ2534Adapter(frame =>
        {
            if (frame.ArbitrationId != 0x7E0 || frame.Data.Length == 0)
                return Array.Empty<Dvinholm.App.CanFrame>();

            var frameType = frame.Data[0] >> 4;
            if (frameType == 0)
            {
                if (Interlocked.Increment(ref requestCount) == 1)
                {
                    // Declares a 10-byte response and starts a multi-frame message.
                    return new[]
                    {
                        new Dvinholm.App.CanFrame(
                            0x7E8, false, false,
                            new byte[] { 0x10, 0x0A, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 },
                            0, 0)
                    };
                }

                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 },
                        0, 0)
                };
            }

            if (frameType == 3 && Volatile.Read(ref requestCount) == 1)
            {
                // After FF, the receiver expects CF sequence 1 (0x21), not 2.
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x22, 0x04, 0x05, 0x06, 0x07 },
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
                FrameTimeout: TimeSpan.FromMilliseconds(300),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(300),
                RequestTimeout: TimeSpan.FromSeconds(1)));

        var error = Assert.Throws<InvalidOperationException>(
            () => channel.Request(new byte[] { 0x22, 0xF1, 0x90 }));

        Assert.Contains("sequence mismatch", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91, 0x42 },
            channel.Request(new byte[] { 0x22, 0xF1, 0x91 }));
        Assert.Equal(2, Volatile.Read(ref requestCount));
    }
}
