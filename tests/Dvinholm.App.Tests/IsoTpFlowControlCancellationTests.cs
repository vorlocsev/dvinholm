using System.Collections.Concurrent;

namespace VolvoJ2534.App.Tests;

public sealed class IsoTpFlowControlCancellationTests
{
    [Fact]
    public async Task Request_CancellationWhileWaitingForFlowControlAllowsNextTransaction()
    {
        var adapter = new FlowControlCancellationAdapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        using var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FlowControlTimeout: TimeSpan.FromSeconds(3),
                FrameTimeout: TimeSpan.FromMilliseconds(500),
                RequestTimeout: TimeSpan.FromSeconds(4)));

        using var cancellation = new CancellationTokenSource();
        var firstRequest = Task.Run(() =>
            channel.Request(Enumerable.Range(0, 20).Select(i => (byte)i).ToArray(), cancellation.Token));

        Assert.True(
            adapter.FirstFrameSent.Wait(TimeSpan.FromSeconds(2)),
            "Multi-frame request did not reach the adapter.");

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await firstRequest);

        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.WriteCount);
    }

    private sealed class FlowControlCancellationAdapter : VolvoJ2534.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _writeCount;

        internal ManualResetEventSlim FirstFrameSent { get; } = new(false);
        internal int WriteCount => Volatile.Read(ref _writeCount);

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

            Interlocked.Increment(ref _writeCount);
            var frameType = frame.Data[0] >> 4;

            if (frameType == 1)
            {
                // Intentionally withhold Flow Control so the caller must wait here.
                FirstFrameSent.Set();
            }
            else if (frameType == 0)
            {
                _incoming.Enqueue(VolvoJ2534.App.CanDecoder.Encode(
                    0x7E8,
                    new byte[] { 0x03, 0x62, 0xF1, 0x91 }));
                _incomingReady.Set();
            }

            return true;
        }

        public void Unload() { }
    }
}
