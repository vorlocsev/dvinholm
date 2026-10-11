using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class UdsDtcRecordResponseTests
{
    [Fact]
    public void ReadDtcByStatusMask_ParsesReturnedDtcRecord()
    {
        var adapter = new DtcRecordAdapter();
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

        var records = client.ReadDtcByStatusMask();

        var record = Assert.Single(records);
        Assert.Equal((uint)0x123456, record.Code);
        Assert.Equal((byte)0x2A, record.Status);
        Assert.Equal(1, adapter.RequestCount);
    }

    private sealed class DtcRecordAdapter : Dvinholm.App.IJ2534Adapter
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

            // Flow Control frames are transport acknowledgements, not UDS requests.
            if ((frame.Data[0] >> 4) != 0)
                return true;

            Interlocked.Increment(ref _requestCount);
            // Single Frame: 7 bytes of UDS payload (SID + six response bytes).
            Enqueue(new byte[] { 0x07, 0x59, 0x02, 0xFF, 0x12, 0x34, 0x56, 0x2A });
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
