using System.Collections.Concurrent;
using System.Diagnostics;

namespace Dvinholm.App.Tests;

public sealed class UdsResponsePendingDeadlineTests
{
    [Fact]
    public async Task ReadDataByIdentifier_RepeatedPendingDoesNotExtendRequestDeadlineAndChannelRecovers()
    {
        var adapter = new RepeatedPendingDeadlineAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromMilliseconds(180)));
        using var client = new Dvinholm.App.UdsClient(channel);

        var stopwatch = Stopwatch.StartNew();
        Assert.Throws<TimeoutException>(() => client.ReadDataByIdentifier(0xF190));
        stopwatch.Stop();

        // ResponsePending is interim, but it must not reset the channel's
        // absolute request deadline indefinitely.
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"Request took too long to time out: {stopwatch.Elapsed}.");

        var data = client.ReadDataByIdentifier(0xF191);

        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42 }, data);
        Assert.Equal(2, adapter.RequestCount);

        await adapter.PendingProducerFinished;
    }

    private sealed class RepeatedPendingDeadlineAdapter : Dvinholm.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<Dvinholm.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private readonly TaskCompletionSource _producerFinished =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);
        internal Task PendingProducerFinished => _producerFinished.Task;

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

            // ISO-TP Flow Control is transport traffic, not a UDS request.
            if ((frame.Data[0] >> 4) != 0)
                return true;

            if (Interlocked.Increment(ref _requestCount) == 1)
            {
                Enqueue(new byte[] { 0x03, 0x7F, 0x22, 0x78 });
                _ = ProducePendingResponses();
            }
            else
            {
                Enqueue(new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 });
            }

            return true;
        }

        private async Task ProducePendingResponses()
        {
            try
            {
                for (var i = 0; i < 10; i++)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(30)).ConfigureAwait(false);
                    Enqueue(new byte[] { 0x03, 0x7F, 0x22, 0x78 });
                }
            }
            finally
            {
                _producerFinished.TrySetResult();
            }
        }

        private void Enqueue(byte[] data)
        {
            _incoming.Enqueue(Dvinholm.App.CanDecoder.Encode(0x7E8, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }
}
