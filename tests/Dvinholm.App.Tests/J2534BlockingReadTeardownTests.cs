namespace Dvinholm.App.Tests;

public sealed class J2534BlockingReadTeardownTests
{
    [Fact]
    public async Task DisconnectDoesNotUnloadAdapterWhileNativeReadIsStillActive()
    {
        using var readEntered = new ManualResetEventSlim();
        using var releaseRead = new ManualResetEventSlim();
        var adapter = new BlockingReadAdapter(readEntered, releaseRead);
        using var session = new Dvinholm.App.J2534Session(adapter);

        Assert.True(session.Connect("virtual-adapter.dll", 500_000, out var error), error);
        Assert.True(readEntered.Wait(TimeSpan.FromSeconds(2)),
            "The receive worker did not enter the adapter's Read call.");

        var disconnect = Task.Run(session.Disconnect);
        try
        {
            // Stop() has a bounded one-second wait. Teardown must still wait
            // beyond that bound instead of unloading a DLL used by Read().
            await Task.Delay(TimeSpan.FromMilliseconds(1200));
            Assert.False(disconnect.IsCompleted,
                "Disconnect returned while the native Read call was still active.");
            Assert.Equal(0, adapter.UnloadCount);
        }
        finally
        {
            releaseRead.Set();
        }

        await disconnect.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.False(session.IsConnected);
        Assert.Equal(1, adapter.UnloadCount);
    }

    private sealed class BlockingReadAdapter(
        ManualResetEventSlim readEntered,
        ManualResetEventSlim releaseRead) : Dvinholm.App.IJ2534SessionAdapter
    {
        private int _unloadCount;
        internal int UnloadCount => Volatile.Read(ref _unloadCount);

        public Dvinholm.App.J2534Native.AdapterCapabilities Capabilities { get; } =
            new(false, false, false);

        public bool Load(string path, out string error)
        {
            error = string.Empty;
            return true;
        }

        public bool Open(out string error)
        {
            error = string.Empty;
            return true;
        }

        public bool Connect(uint baudRate, out string error)
        {
            error = string.Empty;
            return true;
        }

        public Dvinholm.App.J2534Native.AdapterVersionInfo? ReadVersion(out string error)
        {
            error = string.Empty;
            return new("firmware", "dll", "api");
        }

        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            msg = default;
            error = string.Empty;
            readEntered.Set();
            releaseRead.Wait();
            return false;
        }

        public bool Write(in Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            return true;
        }

        public void Unload() => Interlocked.Increment(ref _unloadCount);
    }
}
