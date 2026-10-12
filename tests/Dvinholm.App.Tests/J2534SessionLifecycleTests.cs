namespace Dvinholm.App.Tests;

public sealed class J2534SessionLifecycleTests
{
    [Theory]
    [InlineData(FailurePoint.Open)]
    [InlineData(FailurePoint.Connect)]
    [InlineData(FailurePoint.ReadVersion)]
    public void FailedConnectionReleasesAdapterAndClearsMetadata(FailurePoint failure)
    {
        var adapter = new FakeSessionAdapter { Failure = failure };
        using var session = new Dvinholm.App.J2534Session(adapter);

        Assert.False(session.Connect("virtual-adapter.dll", 500_000, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.False(session.IsConnected);
        Assert.Null(session.AdapterVersion);
        Assert.Null(session.AdapterCapabilities);
        Assert.Equal(2, adapter.UnloadCount);
        Assert.Equal(0, adapter.ReadCount);
    }

    [Fact]
    public void FailedConnectionReportsCleanupFailureWithoutThrowing()
    {
        var adapter = new FakeSessionAdapter
        {
            Failure = FailurePoint.Open,
            ThrowOnUnloadCall = 2
        };
        using var session = new Dvinholm.App.J2534Session(adapter);

        var connected = session.Connect("virtual-adapter.dll", 500_000, out var error);

        Assert.False(connected);
        Assert.Contains("simulated open failure", error);
        Assert.Contains("simulated unload failure", error);
        Assert.False(session.IsConnected);
        Assert.Null(session.AdapterVersion);
        Assert.Null(session.AdapterCapabilities);
    }

    [Fact]
    public void ReconnectReplacesPreviousBusAndKeepsSessionUsable()
    {
        var adapter = new FakeSessionAdapter();
        using var session = new Dvinholm.App.J2534Session(adapter);

        Assert.True(session.Connect("first.dll", 500_000, out var firstError), firstError);
        Assert.True(session.IsConnected);
        var firstBus = session.Bus;

        Assert.True(session.Connect("second.dll", 250_000, out var secondError), secondError);
        Assert.True(session.IsConnected);
        Assert.NotSame(firstBus, session.Bus);
        Assert.Equal(2, adapter.LoadCount);
        Assert.Equal(2, adapter.ConnectCount);
        Assert.Equal(2, adapter.UnloadCount);

        session.Disconnect();
        Assert.False(session.IsConnected);
        Assert.Equal(3, adapter.UnloadCount);
    }

    [Fact]
    public async Task ConcurrentDisconnectWaitsForConnectBeforeUnloadingAdapter()
    {
        using var openEntered = new ManualResetEventSlim();
        using var allowOpenToFinish = new ManualResetEventSlim();
        var adapter = new FakeSessionAdapter
        {
            OpenEntered = openEntered,
            AllowOpenToFinish = allowOpenToFinish
        };
        using var session = new Dvinholm.App.J2534Session(adapter);

        var connectTask = Task.Run(() =>
            session.Connect("virtual-adapter.dll", 500_000, out var error)
                ? string.Empty
                : error);

        Assert.True(openEntered.Wait(TimeSpan.FromSeconds(5)), "Connect did not reach Open.");
        var disconnectTask = Task.Run(session.Disconnect);

        // Connect holds the lifecycle gate while Open is in flight. Disconnect
        // must not unload the DLL until Connect has finished using the adapter.
        await Task.Delay(100);
        Assert.Equal(1, adapter.UnloadCount);

        allowOpenToFinish.Set();
        var connectError = await connectTask.WaitAsync(TimeSpan.FromSeconds(5));
        await disconnectTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(string.Empty, connectError);
        Assert.False(session.IsConnected);
        Assert.Equal(2, adapter.UnloadCount);
    }

    [Fact]
    public async Task ConcurrentDisposeWaitsForConnectAndPermanentlyClosesSession()
    {
        using var openEntered = new ManualResetEventSlim();
        using var allowOpenToFinish = new ManualResetEventSlim();
        var adapter = new FakeSessionAdapter
        {
            OpenEntered = openEntered,
            AllowOpenToFinish = allowOpenToFinish
        };
        var session = new Dvinholm.App.J2534Session(adapter);

        var connectTask = Task.Run(() =>
            session.Connect("virtual-adapter.dll", 500_000, out var error)
                ? string.Empty
                : error);

        Assert.True(openEntered.Wait(TimeSpan.FromSeconds(5)), "Connect did not reach Open.");
        var disposeTask = Task.Run(session.Dispose);

        await Task.Delay(100);
        Assert.Equal(1, adapter.UnloadCount);

        allowOpenToFinish.Set();
        var connectError = await connectTask.WaitAsync(TimeSpan.FromSeconds(5));
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(string.Empty, connectError);
        Assert.False(session.IsConnected);
        Assert.Equal(2, adapter.UnloadCount);
        Assert.Throws<ObjectDisposedException>(() =>
            session.Connect("again.dll", 500_000, out _));
        session.Dispose();
    }

    public enum FailurePoint { None, Open, Connect, ReadVersion }

    private sealed class FakeSessionAdapter : Dvinholm.App.IJ2534SessionAdapter
    {
        private int _unloadCount;
        private int _readCount;
        internal FailurePoint Failure { get; init; }
        internal int ThrowOnUnloadCall { get; init; }
        internal ManualResetEventSlim? OpenEntered { get; init; }
        internal ManualResetEventSlim? AllowOpenToFinish { get; init; }
        internal int LoadCount { get; private set; }
        internal int ConnectCount { get; private set; }
        internal int UnloadCount => Volatile.Read(ref _unloadCount);
        internal int ReadCount => Volatile.Read(ref _readCount);
        public Dvinholm.App.J2534Native.AdapterCapabilities Capabilities { get; } =
            new(false, false, false);

        public bool Load(string path, out string error)
        {
            LoadCount++;
            error = string.Empty;
            return true;
        }
        public bool Open(out string error)
        {
            OpenEntered?.Set();
            AllowOpenToFinish?.Wait(TimeSpan.FromSeconds(5));
            error = Failure == FailurePoint.Open ? "simulated open failure" : string.Empty;
            return Failure != FailurePoint.Open;
        }
        public bool Connect(uint baudRate, out string error)
        {
            ConnectCount++;
            error = Failure == FailurePoint.Connect ? "simulated connect failure" : string.Empty;
            return Failure != FailurePoint.Connect;
        }
        public Dvinholm.App.J2534Native.AdapterVersionInfo? ReadVersion(out string error)
        {
            error = string.Empty;
            if (Failure == FailurePoint.ReadVersion)
                throw new InvalidOperationException("simulated version query failure");
            return new("firmware", "dll", "api");
        }
        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            Interlocked.Increment(ref _readCount);
            msg = default;
            error = string.Empty;
            Thread.Sleep((int)Math.Min(timeout, 5));
            return false;
        }
        public bool Write(in Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            return true;
        }
        public void Unload()
        {
            var count = Interlocked.Increment(ref _unloadCount);
            if (count == ThrowOnUnloadCall)
                throw new InvalidOperationException("simulated unload failure");
        }
    }
}
