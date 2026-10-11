using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class IsoTpFlowControlWaitLimitTests
{
    [Fact]
    public void Request_ExceededFlowControlWaitLimitLeavesChannelReusable()
    {
        var adapter = new FlowControlWaitLimitAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                MaxWaitFlowControls: 1,
                FlowControlTimeout: TimeSpan.FromMilliseconds(300),
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var longRequest = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        var error = Assert.Throws<TimeoutException>(() => channel.Request(longRequest));
        Assert.Contains("WAIT limit exceeded", error.Message, StringComparison.OrdinalIgnoreCase);

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.RequestWriteCount);
    }

    private sealed class FlowControlWaitLimitAdapter : Dvinholm.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<Dvinholm.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestWriteCount;

        internal int RequestWriteCount => Volatile.Read(ref _requestWriteCount);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (_incoming.TryDequeue(out msg))
                return true;

            _incomingReady.WaitOne(TimeSpan.FromMilliseconds(timeout));
            return _incoming.TryDequeue(out msg);
        }

        public bool Write(in Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (!Dvinholm.App.CanDecoder.TryDecode(msg, out var frame, out error))
                return false;

            var frameType = frame.Data[0] >> 4;
            if (frameType == 1)
            {
                Interlocked.Increment(ref _requestWriteCount);
                // ISO-TP Flow Control WAIT: flow status 1, block size 0, STmin 0.
                var waitFrame = Dvinholm.App.CanDecoder.Encode(0x7E8, new byte[] { 0x31, 0x00, 0x00 });
                _incoming.Enqueue(waitFrame);
                _incoming.Enqueue(waitFrame);
                _incomingReady.Set();
            }
            else if (frameType == 0)
            {
                Interlocked.Increment(ref _requestWriteCount);
                _incoming.Enqueue(Dvinholm.App.CanDecoder.Encode(
                    0x7E8,
                    new byte[] { 0x03, 0x62, 0xF1, 0x91 }));
                _incomingReady.Set();
            }

            return true;
        }

        public void Unload() { }
    }
}
