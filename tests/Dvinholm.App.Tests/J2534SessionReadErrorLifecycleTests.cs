namespace Dvinholm.App.Tests;

public sealed class J2534SessionReadErrorLifecycleTests
{
    [Fact]
    public async Task ThrowingReadErrorSubscriberDoesNotPreventLaterSubscribers()
    {
        using var laterSubscriberCalled = new ManualResetEventSlim();
        var adapter = new ReadErrorSessionAdapter();
        using var session = new Dvinholm.App.J2534Session(adapter);
        session.ReadError += _ => throw new InvalidOperationException("simulated UI subscriber failure");
        session.ReadError += _ => laterSubscriberCalled.Set();

        Assert.True(session.Connect("virtual-adapter.dll", 500_000, out var error), error);
        try
        {
            Assert.True(laterSubscriberCalled.Wait(TimeSpan.FromSeconds(3)),
                "A throwing read-error subscriber prevented a later subscriber from receiving the diagnostic.");
            Assert.True(session.IsConnected);
        }
        finally
        {
            session.Disconnect();
        }
    }

    [Fact]
    public async Task DisconnectFromReadErrorCallbackDefersTeardownAndUnloadsAdapter()
    {
        var adapter = new ReadErrorSessionAdapter();
        using var session = new Dvinholm.App.J2534Session(adapter);
        session.ReadError += _ => session.Disconnect();

        Assert.True(session.Connect("virtual-adapter.dll", 500_000, out var error), error);
        Assert.True(session.IsConnected);

        var disconnected = await Task.Run(() => SpinWait.SpinUntil(
            () => !session.IsConnected,
            TimeSpan.FromSeconds(3)));

        Assert.True(disconnected, "Disconnect requested by the RX callback did not complete.");
        Assert.True(adapter.ReadErrorCount >= 1);
        Assert.Equal(2, adapter.UnloadCount);
        Assert.Null(session.AdapterVersion);
        Assert.Null(session.AdapterCapabilities);
    }

    private sealed class ReadErrorSessionAdapter : Dvinholm.App.IJ2534SessionAdapter
    {
        private int _readCount;
        private int _unloadCount;

        internal int ReadErrorCount => Volatile.Read(ref _readCount);
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
            if (Interlocked.Increment(ref _readCount) == 1)
                throw new InvalidOperationException("simulated adapter read failure");

            Thread.Sleep((int)Math.Min(timeout, 5));
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
