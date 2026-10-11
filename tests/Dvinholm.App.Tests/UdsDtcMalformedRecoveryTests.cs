using System.Collections.Concurrent;

namespace VolvoJ2534.App.Tests;

public sealed class UdsDtcMalformedRecoveryTests
{
    [Fact]
    public void ReadDtcByStatusMask_CanSucceedAfterMalformedDtcResponse()
    {
        var adapter = new MalformedThenValidDtcAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromSeconds(1),
                RequestTimeout: TimeSpan.FromSeconds(3)));
        using var client = new VolvoJ2534.App.UdsClient(channel);

        Assert.Throws<InvalidOperationException>(() => client.ReadDtcByStatusMask());

        var records = client.ReadDtcByStatusMask();

        var record = Assert.Single(records);
        Assert.Equal((uint)0x123456, record.Code);
        Assert.Equal((byte)0x2A, record.Status);
        Assert.Equal(2, adapter.RequestCount);
    }

    private sealed class MalformedThenValidDtcAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);

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

            // Flow Control frames are transport acknowledgements, not UDS requests.
            if ((frame.Data[0] >> 4) != 0)
                return true;

            var requestNumber = Interlocked.Increment(ref _requestCount);
            // First reply has a valid UDS envelope but a truncated DTC record.
            // The second reply contains one complete DTC record.
            Enqueue(requestNumber == 1
                ? new byte[] { 0x05, 0x59, 0x02, 0xFF, 0x12, 0x34 }
                : new byte[] { 0x07, 0x59, 0x02, 0xFF, 0x12, 0x34, 0x56, 0x2A });
            return true;
        }

        private void Enqueue(byte[] data)
        {
            _incoming.Enqueue(VolvoJ2534.App.CanDecoder.Encode(0x7E8, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }
}
