using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class IsoTpRequestGateCancellationTests
{
    [Fact]
    public async Task ConcurrentRequest_CancellationWhileWaitingForChannelDoesNotSendRequest()
    {
        var adapter = new DelayedFirstResponseAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromSeconds(1),
                RequestTimeout: TimeSpan.FromSeconds(2)));
        using var client = new Dvinholm.App.UdsClient(channel);

        var firstRequest = Task.Run(() => client.ReadDataByIdentifier(0xF190));
        await adapter.FirstRequestObserved.WaitAsync(TimeSpan.FromSeconds(1));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
        var canceledRequest = Task.Run(() =>
            client.ReadDataByIdentifier(0xF192, cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await canceledRequest.WaitAsync(TimeSpan.FromSeconds(1)));

        var firstData = await firstRequest.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(new byte[] { 0xF1, 0x90, 0x41, 0x42 }, firstData);

        var nextData = client.ReadDataByIdentifier(0xF191);
        Assert.Equal(new byte[] { 0xF1, 0x91, 0x43 }, nextData);

        // The canceled request must not reach the adapter; only request #1
        // and the recovery request may be transmitted.
        Assert.Equal(2, adapter.RequestCount);
    }

    private sealed class DelayedFirstResponseAdapter : Dvinholm.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<Dvinholm.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private readonly TaskCompletionSource _firstRequestObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);
        internal Task FirstRequestObserved => _firstRequestObserved.Task;

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

            // Ignore transport-level frames; this adapter only handles
            // single-frame UDS requests.
            if (frame.Data.Length == 0 || (frame.Data[0] >> 4) != 0)
                return true;

            var requestNumber = Interlocked.Increment(ref _requestCount);
            if (requestNumber == 1)
            {
                _firstRequestObserved.TrySetResult();
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(250),
                    new byte[] { 0x05, 0x62, 0xF1, 0x90, 0x41, 0x42 });
            }
            else
            {
                Enqueue(new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x43 });
            }

            return true;
        }

        private async Task EnqueueAfterDelay(TimeSpan delay, byte[] data)
        {
            await Task.Delay(delay).ConfigureAwait(false);
            Enqueue(data);
        }

        private void Enqueue(byte[] data)
        {
            _incoming.Enqueue(Dvinholm.App.CanDecoder.Encode(0x7E8, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }
}
