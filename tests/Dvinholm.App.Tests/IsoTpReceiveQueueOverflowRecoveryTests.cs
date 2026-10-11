using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class IsoTpReceiveQueueOverflowRecoveryTests
{
    [Fact]
    public void Request_QueueOverflowFailsCurrentTransactionAndNextRequestRecovers()
    {
        using var adapter = new OverflowOnceAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)),
            receiveQueueCapacity: 1);

        var firstError = Assert.Throws<InvalidOperationException>(
            () => channel.Request(new byte[] { 0x22, 0xF1, 0x90 }));

        Assert.Contains("queue dropped", firstError.Message, StringComparison.OrdinalIgnoreCase);

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.RequestCount);
    }

    private sealed class OverflowOnceAdapter : Dvinholm.App.IJ2534Adapter, IDisposable
    {
        private readonly ConcurrentQueue<Dvinholm.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private readonly ManualResetEventSlim _allowFirstReadToProceed = new(false);
        private int _firstRead = 1;
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (Interlocked.Exchange(ref _firstRead, 0) == 1)
                _allowFirstReadToProceed.Wait(TimeSpan.FromSeconds(2));

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

            if ((frame.Data[0] >> 4) != 0)
                return true;

            var requestNumber = Interlocked.Increment(ref _requestCount);
            if (requestNumber == 1)
            {
                // Hold the native reader until the burst is fully queued. This
                // deterministically overflows the one-frame ISO-TP subscription.
                for (var i = 0; i < 128; i++)
                    Enqueue(new byte[] { 0x03, 0x62, 0xF1, (byte)i });

                _allowFirstReadToProceed.Set();
            }
            else
            {
                Enqueue(new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 });
            }

            return true;
        }

        private void Enqueue(byte[] data)
        {
            _incoming.Enqueue(Dvinholm.App.CanDecoder.Encode(0x7E8, data));
            _incomingReady.Set();
        }

        public void Unload() { }

        public void Dispose()
        {
            _allowFirstReadToProceed.Set();
            _allowFirstReadToProceed.Dispose();
            _incomingReady.Dispose();
        }
    }
}
