namespace Dvinholm.App.Tests;

public sealed class CanBusDisposeQueuedSendTests
{
    [Fact]
    public async Task DisposePreventsQueuedSendFromEnteringAdapterAfterActiveWriteCompletes()
    {
        using var writeEntered = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        var adapter = new BlockingWriteAdapter(writeEntered, releaseWrite);
        var bus = new Dvinholm.App.CanBus(adapter);

        var activeSend = Task.Run(() => bus.Send(
            0x7E0, new byte[] { 0x3E, 0x00 }, false,
            TimeSpan.FromSeconds(5), CancellationToken.None, out _));

        Task<bool>? queuedSend = null;
        Task? disposeTask = null;
        try
        {
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(2)),
                "The first native write did not start.");

            queuedSend = Task.Run(() => bus.Send(
                0x7E0, new byte[] { 0x22, 0xF1, 0x90 }, false,
                TimeSpan.FromSeconds(5), CancellationToken.None, out _));

            // Allow the queued operation to reach the transmit-lock wait.
            await Task.Delay(100);
            disposeTask = Task.Run(bus.Dispose);

            // Disposal must wait for the active native call to finish.
            await Task.Delay(100);
            Assert.False(disposeTask.IsCompleted);
        }
        finally
        {
            releaseWrite.Set();
        }

        Assert.True(await activeSend.WaitAsync(TimeSpan.FromSeconds(2)));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await (queuedSend ?? throw new InvalidOperationException("Queued send was not started."))
                .WaitAsync(TimeSpan.FromSeconds(2)));
        await (disposeTask ?? Task.Run(bus.Dispose)).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, adapter.WriteCount);
    }

    [Fact]
    public async Task ConcurrentDisposeWaitsForFirstDisposeToPassNativeWriteBarrier()
    {
        using var writeEntered = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        var adapter = new BlockingWriteAdapter(writeEntered, releaseWrite);
        var bus = new Dvinholm.App.CanBus(adapter);

        var send = Task.Run(() => bus.Send(
            0x7E0, new byte[] { 0x3E, 0x00 }, false,
            TimeSpan.FromSeconds(5), CancellationToken.None, out _));
        Task? firstDispose = null;
        Task? secondDispose = null;
        try
        {
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(2)),
                "The native write did not start.");

            firstDispose = Task.Run(bus.Dispose);
            await Task.Delay(100);
            secondDispose = Task.Run(bus.Dispose);
            await Task.Delay(100);

            Assert.False(firstDispose.IsCompleted,
                "The first Dispose returned while a native write was active.");
            Assert.False(secondDispose.IsCompleted,
                "The concurrent Dispose returned before the first disposal completed.");
        }
        finally
        {
            releaseWrite.Set();
        }

        Assert.True(await send.WaitAsync(TimeSpan.FromSeconds(2)));
        await (firstDispose ?? Task.Run(bus.Dispose)).WaitAsync(TimeSpan.FromSeconds(2));
        await (secondDispose ?? Task.Run(bus.Dispose)).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, adapter.WriteCount);
    }
    private sealed class BlockingWriteAdapter(
        ManualResetEventSlim writeEntered,
        ManualResetEventSlim releaseWrite) : Dvinholm.App.IJ2534Adapter
    {
        private int _writeCount;
        internal int WriteCount => Volatile.Read(ref _writeCount);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }
        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            msg = default;
            error = string.Empty;
            Thread.Sleep((int)Math.Min(timeout, 5));
            return false;
        }

        public bool Write(in Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            Interlocked.Increment(ref _writeCount);
            writeEntered.Set();
            releaseWrite.Wait();
            error = string.Empty;
            return true;
        }

        public void Unload() { }
    }
}
