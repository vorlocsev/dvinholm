using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class DisposeCancelsActiveUdsRequestTests
{
    [Fact]
    public async Task DisposingClient_CancelsInFlightRequestPromptly()
    {
        var adapter = new NoResponseAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromSeconds(5),
                RequestTimeout: TimeSpan.FromSeconds(10)));
        var client = new Dvinholm.App.UdsClient(channel);

        var request = Task.Run(() => client.ReadDataByIdentifier(0xF190));
        await adapter.RequestObserved.WaitAsync(TimeSpan.FromSeconds(1));

        // Disposal must cancel the receive wait before tearing down the RX
        // subscription, rather than waiting for the configured timeout.
        client.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await request.WaitAsync(TimeSpan.FromSeconds(1)));

        Assert.Equal(1, adapter.RequestCount);
        channel.Dispose();
    }

    private sealed class NoResponseAdapter : Dvinholm.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<Dvinholm.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private readonly TaskCompletionSource _requestObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);
        internal Task RequestObserved => _requestObserved.Task;

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

            if (frame.Data.Length > 0 && (frame.Data[0] >> 4) == 0)
            {
                Interlocked.Increment(ref _requestCount);
                _requestObserved.TrySetResult();
            }

            // Intentionally do not provide an ECU response.
            return true;
        }

        public void Unload() { }
    }
}
