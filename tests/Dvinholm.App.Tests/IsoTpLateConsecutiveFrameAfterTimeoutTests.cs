using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class IsoTpLateConsecutiveFrameAfterTimeoutTests
{
    [Fact]
    public void Request_IgnoresLateConsecutiveFrameFromTimedOutResponse()
    {
        var adapter = new LateConsecutiveFrameAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(100),
                RequestTimeout: TimeSpan.FromSeconds(2)));

        var firstError = Assert.Throws<TimeoutException>(
            () => channel.Request(new byte[] { 0x22, 0xF1, 0x90 }));
        Assert.Contains("receive timeout", firstError.Message, StringComparison.OrdinalIgnoreCase);

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.RequestCount);
    }

    private sealed class LateConsecutiveFrameAdapter : Dvinholm.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<Dvinholm.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);

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
            if (frameType == 3)
            {
                // This CF belongs to the first response, but arrives only after
                // the receiver's consecutive-frame timeout has expired.
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(180),
                    new byte[] { 0x21, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17 });
            }
            else if (frameType == 0)
            {
                if (Interlocked.Increment(ref _requestCount) == 1)
                {
                    Enqueue(new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 });
                }
                else
                {
                    // Ensure the stale CF is delivered first during request #2.
                    _ = EnqueueAfterDelay(
                        TimeSpan.FromMilliseconds(250),
                        new byte[] { 0x03, 0x62, 0xF1, 0x91 });
                }
            }

            return true;
        }

        private async Task EnqueueAfterDelay(TimeSpan delay, byte[] data)
        {
            await Task.Delay(delay).ConfigureAwait(false);
            Enqueue(data);
        }

        private void Enqueue(byte[] data)
        {
            _incoming.Enqueue(Dvinholm.App.CanDecoder.Encode(0x7E8, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }
}
