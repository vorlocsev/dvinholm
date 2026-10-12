using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class UdsPendingThenUnrelatedNegativeResponseTests
{
    [Fact]
    public void ReadDataByIdentifier_IgnoresUnrelatedNegativeResponseAfterPending()
    {
        var adapter = new PendingThenUnrelatedNegativeAdapter();
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

        var response = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42 }, response);
        Assert.Equal(1, adapter.RequestCount);
    }

    private sealed class PendingThenUnrelatedNegativeAdapter : Dvinholm.App.IJ2534Adapter
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

            // Only a single-frame UDS request starts this scripted exchange.
            if (frame.Data.Length == 0 || (frame.Data[0] >> 4) != 0)
                return true;

            Interlocked.Increment(ref _requestCount);

            // The ECU keeps the requested service pending, then an unrelated
            // negative response arrives on the same CAN response ID.
            Enqueue(new byte[] { 0x03, 0x7F, 0x22, 0x78 });
            Enqueue(new byte[] { 0x03, 0x7F, 0x19, 0x31 });

            // The actual response for ReadDataByIdentifier DID 0xF191 follows.
            Enqueue(new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 });
            return true;
        }

        private void Enqueue(byte[] data)
        {
            _incoming.Enqueue(Dvinholm.App.CanDecoder.Encode(0x7E8, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }
}
