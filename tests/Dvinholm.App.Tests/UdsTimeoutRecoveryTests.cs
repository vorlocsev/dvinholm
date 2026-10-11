using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class UdsTimeoutRecoveryTests
{
    [Fact]
    public void ReadDataByIdentifier_LateResponseFromTimedOutRequestIsIgnored()
    {
        var adapter = new LateUdsResponseAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromMilliseconds(120)));
        using var client = new Dvinholm.App.UdsClient(channel);

        Assert.Throws<TimeoutException>(() => client.ReadDataByIdentifier(0xF190));

        var data = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42 }, data);
        Assert.Equal(2, adapter.RequestCount);
    }

    private sealed class LateUdsResponseAdapter : Dvinholm.App.IJ2534Adapter
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

            // Flow Control is a transport acknowledgement, not a new UDS request.
            if ((frame.Data[0] >> 4) != 0)
                return true;

            var requestNumber = Interlocked.Increment(ref _requestCount);
            if (requestNumber == 1)
            {
                // This response arrives after request #1 has timed out and while
                // request #2 is waiting. Its DID must not satisfy the new request.
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(170),
                    new byte[] { 0x04, 0x62, 0xF1, 0x90, 0xAA });
            }
            else
            {
                // Arrive after the stale response so the test verifies correlation,
                // not merely that the correct response happened to be read first.
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(100),
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
