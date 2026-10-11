using System.Diagnostics;

namespace Dvinholm.App.Tests;

public sealed class IsoTpFlowControlDeadlineTests
{
    [Fact]
    public void Request_UnrelatedFramesDoNotExtendFlowControlTimeout()
    {
        var adapter = new NoisyFlowControlAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FlowControlTimeout: TimeSpan.FromMilliseconds(120),
                FrameTimeout: TimeSpan.FromSeconds(1),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var request = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        var elapsed = Stopwatch.StartNew();

        var error = Assert.Throws<TimeoutException>(() => channel.Request(request));

        Assert.Equal("ISO-TP receive timeout expired.", error.Message);
        Assert.True(elapsed.Elapsed < TimeSpan.FromMilliseconds(300),
            $"Flow Control timeout was extended by unrelated frames: {elapsed.Elapsed}.");
        // No Consecutive Frame may be sent until the receiver grants Flow Control.
        Assert.Equal(0, adapter.ConsecutiveFrameWrites);
    }

    private sealed class NoisyFlowControlAdapter : Dvinholm.App.IJ2534Adapter
    {
        private long _noiseUntil;
        private int _consecutiveFrameWrites;

        internal int ConsecutiveFrameWrites => Volatile.Read(ref _consecutiveFrameWrites);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            var until = Volatile.Read(ref _noiseUntil);
            if (until != 0 && Stopwatch.GetTimestamp() < until)
            {
                Thread.Sleep(5);
                msg = Dvinholm.App.CanDecoder.Encode(
                    0x7E8,
                    new byte[] { 0x03, 0x62, 0xF1, 0x91 });
                return true;
            }

            msg = default;
            Thread.Sleep(TimeSpan.FromMilliseconds(Math.Min(timeout, 10)));
            return false;
        }

        public bool Write(in Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (!Dvinholm.App.CanDecoder.TryDecode(msg, out var frame, out error))
                return false;

            var type = frame.Data[0] >> 4;
            if (type == 1)
            {
                Volatile.Write(ref _noiseUntil,
                    Stopwatch.GetTimestamp() + (long)(0.4 * Stopwatch.Frequency));
            }
            else if (type == 2)
            {
                Interlocked.Increment(ref _consecutiveFrameWrites);
            }

            return true;
        }

        public void Unload() { }
    }
}
