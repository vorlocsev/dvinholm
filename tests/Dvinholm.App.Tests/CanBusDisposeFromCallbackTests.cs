namespace Dvinholm.App.Tests;

public sealed class CanBusDisposeFromCallbackTests
{
    [Fact]
    public async Task DisposeFromReadErrorCallbackDefersCleanupAndExternalCallerWaits()
    {
        var adapter = new BlockingReadErrorAdapter();
        var bus = new Dvinholm.App.CanBus(adapter);
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var externalDisposeStarted = new ManualResetEventSlim();

        bus.ReadError += error =>
        {
            if (!error.Message.Contains("simulated read failure", StringComparison.Ordinal))
                return;

            // This callback runs synchronously on the receive worker. The
            // bus must defer teardown rather than joining its own worker.
            bus.Dispose();
            callbackEntered.Set();
            releaseCallback.Wait();
        };

        bus.Start();
        Task? externalDispose = null;

        try
        {
            Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(2)),
                "The read-error callback did not reach the disposal barrier.");

            externalDispose = Task.Run(() =>
            {
                externalDisposeStarted.Set();
                bus.Dispose();
            });

            Assert.True(externalDisposeStarted.Wait(TimeSpan.FromSeconds(1)),
                "The external Dispose caller did not start.");
            await Task.Delay(100);
            Assert.False(externalDispose.IsCompleted,
                "External Dispose returned while the receive callback was still active.");
        }
        finally
        {
            releaseCallback.Set();
        }

        await (externalDispose ?? Task.Run(bus.Dispose)).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, adapter.ReadCount);
        Assert.Throws<ObjectDisposedException>(() => bus.Start());
    }

    private sealed class BlockingReadErrorAdapter : Dvinholm.App.IJ2534Adapter
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
