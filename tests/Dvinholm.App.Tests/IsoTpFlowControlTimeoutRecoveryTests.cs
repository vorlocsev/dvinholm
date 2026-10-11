using System.Collections.Concurrent;

namespace VolvoJ2534.App.Tests;

public sealed class IsoTpFlowControlTimeoutRecoveryTests
{
    [Fact]
    public void Request_FlowControlTimeoutLeavesChannelReusable()
    {
        var adapter = new FlowControlTimeoutAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FlowControlTimeout: TimeSpan.FromMilliseconds(120),
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var longRequest = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        var error = Assert.Throws<TimeoutException>(() => channel.Request(longRequest));
        Assert.Equal("ISO-TP receive timeout expired.", error.Message);

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.WriteCount);
    }

    private sealed class FlowControlTimeoutAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _writeCount;

        internal int WriteCount => Volatile.Read(ref _writeCount);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (_incoming.TryDequeue(out msg))
                return true;

            _incomingReady.WaitOne(TimeSpan.FromMilliseconds(timeout));
            return _incoming.TryDequeue(out msg);
        }

        public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (!VolvoJ2534.App.CanDecoder.TryDecode(msg, out var frame, out error))
                return false;

            Interlocked.Increment(ref _writeCount);
            if ((frame.Data[0] >> 4) == 0)
            {
                _incoming.Enqueue(VolvoJ2534.App.CanDecoder.Encode(
                    0x7E8,
                    new byte[] { 0x03, 0x62, 0xF1, 0x91 }));
                _incomingReady.Set();
            }

            // No Flow Control is sent for the first frame. The initial request
            // must time out without sending any Consecutive Frames.
            return true;
        }

        public void Unload() { }
    }
}
