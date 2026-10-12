using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class IsoTpLateFlowControlAfterCancellationTests
{
    [Fact]
    public async Task Request_IgnoresLateFlowControlAfterCancellationDuringNextResponse()
    {
        var adapter = new LateFlowControlAfterCancellationAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FlowControlTimeout: TimeSpan.FromSeconds(1),
                FrameTimeout: TimeSpan.FromSeconds(1),
                RequestTimeout: TimeSpan.FromSeconds(3)));

        using var cancellation = new CancellationTokenSource();
        var firstRequest = Task.Run(() =>
            channel.Request(Enumerable.Range(0, 20).Select(i => (byte)i).ToArray(), cancellation.Token));

        Assert.True(
            adapter.FirstFrameSent.Wait(TimeSpan.FromSeconds(2)),
            "Multi-frame request did not reach the adapter.");

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await firstRequest);

        // The adapter delivers an old FC while the next transaction is waiting
        // for its own response. It must not terminate or corrupt that response.
        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.RequestCount);
        Assert.True(adapter.LateFlowControlSent);
    }

    private sealed class LateFlowControlAfterCancellationAdapter : Dvinholm.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<Dvinholm.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;
        private int _lateFlowControlSent;

        internal ManualResetEventSlim FirstFrameSent { get; } = new(false);
        internal int RequestCount => Volatile.Read(ref _requestCount);
        internal bool LateFlowControlSent => Volatile.Read(ref _lateFlowControlSent) != 0;

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

            var frameType = frame.Data[0] >> 4;
            if (frameType == 1)
            {
                // Intentionally withhold Flow Control for the first request.
                FirstFrameSent.Set();
            }
            else if (frameType == 0)
            {
                if (Interlocked.Increment(ref _requestCount) == 1)
                    return true;

                // A stale Flow Control from the cancelled transaction arrives
                // after the second request is sent but before its real response.
                Enqueue(new byte[] { 0x30, 0x00, 0x00 });
                Interlocked.Exchange(ref _lateFlowControlSent, 1);
                _ = EnqueueAfterDelay(
                    TimeSpan.FromMilliseconds(200),
                    new byte[] { 0x03, 0x62, 0xF1, 0x91 });
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
