namespace Dvinholm.App.Tests;

public sealed class CanRxDispatcherStopFromCallbackTests
{
    [Fact]
    public async Task StopFromReadErrorHandlerStopsCurrentWorkerAndAllowsRestart()
    {
        var adapter = new ThrowOnceReadAdapter();
        using var dispatcher = new Dvinholm.App.CanRxDispatcher(adapter);
        using var stopReturned = new ManualResetEventSlim();

        dispatcher.ReadError += error =>
        {
            if (!error.Message.Contains("J2534 read failed due to an unexpected adapter error.", StringComparison.Ordinal))
                return;

            // Stop is invoked on the receive worker itself. It must request
            // cancellation without synchronously joining the current worker.
            dispatcher.Stop();
            stopReturned.Set();
        };

        dispatcher.Start();

        Assert.True(stopReturned.Wait(TimeSpan.FromSeconds(2)),
            "Stop from the read-error callback did not return.");
        Assert.Equal(1, adapter.ReadCount);

        // The first worker needs to finish unwinding its error handler before
        // Start can safely create another native reader.
        var restarted = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                dispatcher.Start();
                restarted = true;
                break;
            }
            catch (InvalidOperationException)
            {
                await Task.Delay(10);
            }
        }

        Assert.True(restarted, "The stopped receive worker did not exit in time.");

        var readDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (adapter.ReadCount < 2 && DateTime.UtcNow < readDeadline)
            await Task.Delay(10);

        Assert.True(adapter.ReadCount >= 2, "The restarted worker did not read from the adapter.");
        dispatcher.Stop();
    }

    private sealed class ThrowOnceReadAdapter : Dvinholm.App.IJ2534Adapter
    {
        private int _readCount;
        private int _throwNextRead = 1;

        internal int ReadCount => Volatile.Read(ref _readCount);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            msg = default;
            Interlocked.Increment(ref _readCount);
            if (Interlocked.Exchange(ref _throwNextRead, 0) == 1)
                throw new InvalidOperationException("simulated adapter disconnect");

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
