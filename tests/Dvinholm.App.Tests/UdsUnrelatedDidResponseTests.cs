using System.Collections.Concurrent;

namespace VolvoJ2534.App.Tests;

public sealed class UdsUnrelatedDidResponseTests
{
    [Fact]
    public void ReadDataByIdentifier_IgnoresResponseForDifferentDid()
    {
        var adapter = new UnrelatedDidAdapter(
            new byte[] { 0x04, 0x62, 0xF1, 0x90, 0x00 },
            new byte[] { 0x05, 0x62, 0xF1, 0x91, 0x42, 0x43 });
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

        var data = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42, 0x43 }, data);
        Assert.Equal(1, adapter.RequestCount);
    }

    [Fact]
    public void ReadDataByIdentifier_IgnoresNegativeResponseForDifferentService()
    {
        var adapter = new UnrelatedDidAdapter(
            new byte[] { 0x03, 0x7F, 0x19, 0x31 },
            new byte[] { 0x05, 0x62, 0xF1, 0x91, 0x42, 0x43 });
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

        var data = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42, 0x43 }, data);
        Assert.Equal(1, adapter.RequestCount);
    }

    [Fact]
    public void ReadDataByIdentifier_ThrowsForNegativeResponseToRequestedService()
    {
        var adapter = new UnrelatedDidAdapter(new byte[] { 0x03, 0x7F, 0x22, 0x31 });
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

        var exception = Assert.Throws<VolvoJ2534.App.UdsNegativeResponseException>(
            () => client.ReadDataByIdentifier(0xF191));

        Assert.Equal((byte)0x22, exception.RequestedService);
        Assert.Equal((byte)0x31, exception.NegativeResponseCode);
        Assert.Equal(1, adapter.RequestCount);
    }

    private sealed class UnrelatedDidAdapter(params byte[][] responses) : VolvoJ2534.App.IJ2534Adapter
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
            if ((frame.Data[0] >> 4) == 3)
                return true;
            if ((frame.Data[0] >> 4) != 0)
                return true;

            Interlocked.Increment(ref _requestCount);
            foreach (var response in responses)
                Enqueue(response);
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
