using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class UdsLateResponseAfterCancellationTests
{
    [Fact]
    public void ReadDataByIdentifier_LateResponseAfterCancellationDoesNotSatisfyNextRequest()
    {
        var adapter = new LateResponseAfterCancellationAdapter();
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
            Assert.ThrowsAny<OperationCanceledException>(
                () => client.ReadDataByIdentifier(0xF190, cancellation.Token));
        }

        var data = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42 }, data);
        Assert.Equal(2, adapter.RequestCount);
    }

    private sealed class LateResponseAfterCancellationAdapter : Dvinholm.App.IJ2534Adapter
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

            // Flow Control is transport-level traffic, not a new UDS request.
            if ((frame.Data[0] >> 4) != 0)
                return true;

            if (Interlocked.Increment(ref _requestCount) == 1)
            {
                // The ECU says it is still processing. Its positive response is
                // deliberately delayed until after the caller cancels.
                Enqueue(new byte[] { 0x03, 0x7F, 0x22, 0x78 });
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(180),
                    new byte[] { 0x04, 0x62, 0xF1, 0x90, 0xAA });
            }
            else
            {
                // This response must win even though the stale F190 response
                // arrives first while the F191 request is active.
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(140),
                    new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 });
            }

            return true;
        }

        private void Enqueue(byte[] data)
        {
            _incoming.Enqueue(Dvinholm.App.CanDecoder.Encode(0x7E8, data));
            _incomingReady.Set();
        }

        private async Task EnqueueAfterDelay(TimeSpan delay, byte[] data)
        {
            await Task.Delay(delay).ConfigureAwait(false);
            Enqueue(data);
        }

        public void Unload() { }
    }
}
