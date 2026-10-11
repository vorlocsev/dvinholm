using System.Collections.Concurrent;

namespace VolvoJ2534.App.Tests;

public sealed class UdsUnrelatedDtcResponseTests
{
    [Fact]
    public void ReadDtcByStatusMask_IgnoresResponseForDifferentSubfunction()
    {
        var adapter = new UnrelatedDtcSubfunctionAdapter();
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

        var records = client.ReadDtcByStatusMask();

        Assert.Empty(records);
        Assert.Equal(1, adapter.RequestCount);
    }

    private sealed class UnrelatedDtcSubfunctionAdapter : VolvoJ2534.App.IJ2534Adapter
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

            // Flow Control frames are not new UDS requests.
            if ((frame.Data[0] >> 4) != 0)
                return true;

            Interlocked.Increment(ref _requestCount);

            // 0x59 0x01 belongs to another ReadDTCInformation subfunction.
            Enqueue(new byte[] { 0x03, 0x59, 0x01, 0x00 });
            // 0x59 0x02 is the requested reportDTCByStatusMask response.
            Enqueue(new byte[] { 0x03, 0x59, 0x02, 0xFF });
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
