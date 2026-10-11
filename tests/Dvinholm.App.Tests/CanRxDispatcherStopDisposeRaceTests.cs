namespace Dvinholm.App.Tests;

public sealed class CanRxDispatcherStopDisposeRaceTests
{
    [Fact]
    public async Task DisposeWaitsForWorkerWhenStopWasCalledFromReadErrorCallback()
    {
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var callbackReturned = new ManualResetEventSlim();
        var adapter = new BlockingReadErrorAdapter();
        var dispatcher = new Dvinholm.App.CanRxDispatcher(adapter);

        dispatcher.ReadError += error =>
        {
            if (!error.Message.Contains("simulated adapter disconnect", StringComparison.Ordinal))
                return;

            dispatcher.Stop();
            callbackEntered.Set();
            releaseCallback.Wait();
            callbackReturned.Set();
        };

        dispatcher.Start();

        try
        {
            Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(2)),
                "The read-error callback did not enter after Stop returned.");

            var disposeStarted = new ManualResetEventSlim();
            var disposeTask = Task.Run(() =>
            {
                disposeStarted.Set();
                dispatcher.Dispose();
            });

            Assert.True(disposeStarted.Wait(TimeSpan.FromSeconds(1)),
                "The concurrent Dispose operation did not start.");

            // Dispose must not return while the receive worker is still inside
            // a callback, even though that callback already called Stop().
            await Task.Delay(100);
            Assert.False(disposeTask.IsCompleted,
                "Dispose returned before the receive worker left its callback.");

            releaseCallback.Set();
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(callbackReturned.IsSet);
        }
        finally
        {
            releaseCallback.Set();
            dispatcher.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentDisposeWaitsWhenFirstDisposeWasCalledFromReadErrorCallback()
    {
        using var callbackDisposed = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var callbackReturned = new ManualResetEventSlim();
        var adapter = new BlockingReadErrorAdapter();
        var dispatcher = new Dvinholm.App.CanRxDispatcher(adapter);

        dispatcher.ReadError += error =>
        {
            if (!error.Message.Contains("simulated adapter disconnect", StringComparison.Ordinal))
                return;

            dispatcher.Dispose();
            callbackDisposed.Set();
            releaseCallback.Wait();
            callbackReturned.Set();
        };

        dispatcher.Start();
        try
        {
            Assert.True(callbackDisposed.Wait(TimeSpan.FromSeconds(2)),
                "Dispose from the read-error callback did not return without deadlocking.");

            var externalDisposeStarted = new ManualResetEventSlim();
            var externalDispose = Task.Run(() =>
            {
                externalDisposeStarted.Set();
                dispatcher.Dispose();
            });

            Assert.True(externalDisposeStarted.Wait(TimeSpan.FromSeconds(1)));
            await Task.Delay(100);
            Assert.False(externalDispose.IsCompleted,
                "A concurrent Dispose returned before deferred worker cleanup completed.");

            releaseCallback.Set();
            await externalDispose.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(callbackReturned.IsSet);
        }
        finally
        {
            releaseCallback.Set();
            dispatcher.Dispose();
        }
    }
    private sealed class BlockingReadErrorAdapter : Dvinholm.App.IJ2534Adapter
    {
        private int _readCount;

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            msg = default;
            error = string.Empty;
            if (Interlocked.Increment(ref _readCount) == 1)
                throw new InvalidOperationException("simulated adapter disconnect");

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
