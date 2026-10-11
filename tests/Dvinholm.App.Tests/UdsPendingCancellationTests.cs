using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class UdsPendingCancellationTests
{
    [Fact]
    public void ReadDataByIdentifier_CancellationAfterResponsePendingAllowsNextRequest()
    {
        var adapter = new PendingCancellationRecoveryAdapter();
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

        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150)))
        {
            Assert.ThrowsAny<OperationCanceledException>(() =>
                client.ReadDataByIdentifier(0xF190, cancellation.Token));
        }

        var data = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91 }, data);
        Assert.Equal(2, adapter.RequestCount);
    }

    private sealed class PendingCancellationRecoveryAdapter : Dvinholm.App.IJ2534Adapter
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

            // ISO-TP Flow Control is not a new UDS request.
            if ((frame.Data[0] >> 4) == 3)
                return true;

            if ((frame.Data[0] >> 4) != 0)
                return true;

            if (Interlocked.Increment(ref _requestCount) == 1)
            {
                // UDS ResponsePending: the ECU accepted the first request but
                // never sends a final response before the caller cancels.
                Enqueue(new byte[] { 0x03, 0x7F, 0x22, 0x78 });
            }
            else
            {
                Enqueue(new byte[] { 0x03, 0x62, 0xF1, 0x91 });
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
