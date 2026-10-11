namespace Dvinholm.App.Tests;

public sealed class CanRxDispatcherWorkerDisposalTests
{
    [Fact]
    public async Task DisposeFromReadErrorHandlerDoesNotDeadlockReceiveWorker()
    {
        var adapter = new ThrowOnceReadAdapter();
        var dispatcher = new Dvinholm.App.CanRxDispatcher(adapter);
        using var disposeHandlerReturned = new ManualResetEventSlim();

        dispatcher.ReadError += error =>
        {
            if (!error.Message.Contains("simulated read failure", StringComparison.Ordinal))
                return;

            // ReadError is raised synchronously on the RX worker. Disposing
            // here must not wait for that same worker to finish itself.
            dispatcher.Dispose();
            disposeHandlerReturned.Set();
        };

        dispatcher.Start();

        Assert.True(disposeHandlerReturned.Wait(TimeSpan.FromSeconds(2)),
            "Dispose from the read-error callback deadlocked the receive worker.");

        // The worker must observe cancellation after the callback and exit,
        // rather than issuing another adapter read after disposal.
        await Task.Delay(100);
        Assert.Equal(1, adapter.ReadCount);
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Start());

        // Safe when the callback has already initiated disposal.
        dispatcher.Dispose();
    }

    private sealed class ThrowOnceReadAdapter : Dvinholm.App.IJ2534Adapter
    {
        private int _readCount;
        internal int ReadCount => Volatile.Read(ref _readCount);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            msg = default;
            var count = Interlocked.Increment(ref _readCount);
            if (count == 1)
                throw new InvalidOperationException("simulated read failure");

            error = string.Empty;
            Thread.Sleep((int)Math.Min(timeout, 5));
            return false;
        }

        public bool Write(in Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            return true;
        }

        public void Unload() { }
    }
}
