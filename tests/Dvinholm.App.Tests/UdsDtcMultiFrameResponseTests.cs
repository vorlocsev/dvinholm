using System.Collections.Concurrent;

namespace VolvoJ2534.App.Tests;

public sealed class UdsDtcMultiFrameResponseTests
{
    [Fact]
    public void ReadDtcByStatusMask_ParsesMultipleRecordsAcrossIsoTpFrames()
    {
        var adapter = new MultiFrameDtcAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromSeconds(1),
                ConsecutiveFrameTimeout: TimeSpan.FromSeconds(1),
                RequestTimeout: TimeSpan.FromSeconds(3)));
        using var client = new VolvoJ2534.App.UdsClient(channel);

        var records = client.ReadDtcByStatusMask();

        Assert.Equal(2, records.Count);
        Assert.Equal((uint)0x123456, records[0].Code);
        Assert.Equal((byte)0x2A, records[0].Status);
        Assert.Equal((uint)0xABCDEF, records[1].Code);
        Assert.Equal((byte)0x08, records[1].Status);
        Assert.Equal(1, adapter.RequestCount);
        Assert.Equal(1, adapter.FlowControlCount);
    }

    private sealed class MultiFrameDtcAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;
        private int _flowControlCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);
        internal int FlowControlCount => Volatile.Read(ref _flowControlCount);

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

            var pciType = frame.Data[0] >> 4;
            if (pciType == 3)
            {
                Interlocked.Increment(ref _flowControlCount);
                // ECU continues the response only after the tester's Flow Control.
                Enqueue(new byte[] { 0x21, 0x2A, 0xAB, 0xCD, 0xEF, 0x08, 0x00, 0x00 });
                return true;
            }

            if (pciType != 0)
                return true;

            Interlocked.Increment(ref _requestCount);
            // 11-byte UDS response: subfunction/status mask + two four-byte DTC records.
            Enqueue(new byte[] { 0x10, 0x0B, 0x59, 0x02, 0xFF, 0x12, 0x34, 0x56 });
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
