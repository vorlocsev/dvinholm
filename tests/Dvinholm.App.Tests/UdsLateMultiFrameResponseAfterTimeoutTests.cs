using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class UdsLateMultiFrameResponseAfterTimeoutTests
{
    [Fact]
    public void ReadDataByIdentifier_LateMultiFrameResponseAfterTimeoutIsCorrelated()
    {
        var adapter = new LateMultiFrameTimeoutAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromSeconds(1),
                ConsecutiveFrameTimeout: TimeSpan.FromSeconds(1),
                RequestTimeout: TimeSpan.FromMilliseconds(400)));
        using var client = new Dvinholm.App.UdsClient(channel);

        Assert.Throws<TimeoutException>(() => client.ReadDataByIdentifier(0xF190));

        // The old multi-frame F190 response arrives during request #2. It must
        // be reassembled at ISO-TP level, then rejected by UDS DID correlation.
        var data = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42 }, data);
        Assert.Equal(2, adapter.RequestCount);
        Assert.Equal(1, adapter.StaleResponseFlowControlCount);
    }

    private sealed class LateMultiFrameTimeoutAdapter : Dvinholm.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<Dvinholm.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;
        private int _staleResponseFlowControlCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);
        internal int StaleResponseFlowControlCount => Volatile.Read(ref _staleResponseFlowControlCount);

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
                // Acknowledge the stale F190 First Frame and send its CF.
                Interlocked.Increment(ref _staleResponseFlowControlCount);
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(25),
                    new byte[] { 0x21, 0x04, 0x05, 0x06, 0x07, 0x08 });
                return true;
            }

            if (frameType != 0)
                return true;

            var requestNumber = Interlocked.Increment(ref _requestCount);
            if (requestNumber == 1)
            {
                // Arrives after request #1 has timed out, while request #2 is active.
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(500),
                    new byte[] { 0x10, 0x0B, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 });
            }
            else
            {
                // Arrive after the stale multi-frame response, but before #2 times out.
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(300),
                    new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 });
            }

            return true;
        }

        private async Task EnqueueAfterDelay(TimeSpan delay, byte[] data)
        {
            await Task.Delay(delay).ConfigureAwait(false);
            _incoming.Enqueue(Dvinholm.App.CanDecoder.Encode(0x7E8, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }
}
