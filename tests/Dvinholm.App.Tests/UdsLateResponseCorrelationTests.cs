using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class UdsLateResponseCorrelationTests
{
    [Fact]
    public void LateResponseFromCanceledRequest_IsIgnoredDuringNextRequest()
    {
        var adapter = new LateResponseAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(2)));
        using var client = new Dvinholm.App.UdsClient(channel);

        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
        {
            Assert.ThrowsAny<OperationCanceledException>(() =>
                client.ReadDataByIdentifier(0xF190, cancellation.Token));
        }

        // The canceled transaction's delayed response arrives after this
        // request starts. UDS DID correlation must discard it and keep waiting.
        var response = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91, 0x43, 0x44 }, response);
        Assert.Equal(2, adapter.RequestCount);
    }

    private sealed class LateResponseAdapter : Dvinholm.App.IJ2534Adapter
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

            if (frame.Data.Length == 0 || (frame.Data[0] >> 4) != 0)
                return true;

            var requestNumber = Interlocked.Increment(ref _requestCount);
            if (requestNumber == 1)
            {
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(180),
                    new byte[] { 0x05, 0x62, 0xF1, 0x90, 0x41, 0x42 });
            }
            else
            {
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(250),
                    new byte[] { 0x05, 0x62, 0xF1, 0x91, 0x43, 0x44 });
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
