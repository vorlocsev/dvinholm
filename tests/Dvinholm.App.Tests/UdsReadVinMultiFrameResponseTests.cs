using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class UdsReadVinMultiFrameResponseTests
{
    [Fact]
    public void ReadVin_ReassemblesMultiFrameResponseAndValidatesDid()
    {
        var adapter = new MultiFrameVinAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromSeconds(1),
                ConsecutiveFrameTimeout: TimeSpan.FromSeconds(1),
                RequestTimeout: TimeSpan.FromSeconds(3)));
        using var client = new Dvinholm.App.UdsClient(channel);

        var vin = client.ReadVin();

        Assert.Equal("YV1ABCDEF12345678", vin);
        Assert.Equal(1, adapter.RequestCount);
        Assert.Equal(1, adapter.FlowControlCount);
    }

    private sealed class MultiFrameVinAdapter : Dvinholm.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<Dvinholm.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;
        private int _flowControlCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);
        internal int FlowControlCount => Volatile.Read(ref _flowControlCount);

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

            var pciType = frame.Data[0] >> 4;
            if (pciType == 3)
            {
                Interlocked.Increment(ref _flowControlCount);
                Enqueue(new byte[] { 0x21, (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', (byte)'1' });
                Enqueue(new byte[] { 0x22, (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8' });
            }
            else if (pciType == 0)
            {
                Interlocked.Increment(ref _requestCount);
                // Positive response is 20 bytes: SID + DID + the 17-character VIN.
                Enqueue(new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, (byte)'Y', (byte)'V', (byte)'1' });
            }

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
