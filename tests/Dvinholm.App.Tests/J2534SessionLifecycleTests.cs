namespace Dvinholm.App.Tests;

public sealed class J2534SessionLifecycleTests
{
    [Theory]
    [InlineData(FailurePoint.Open)]
    [InlineData(FailurePoint.Connect)]
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

    public enum FailurePoint { None, Open, Connect }

    private sealed class FakeSessionAdapter : Dvinholm.App.IJ2534SessionAdapter
    {
        private int _unloadCount;
        private int _readCount;
        internal FailurePoint Failure { get; init; }
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
        public void Unload() => Interlocked.Increment(ref _unloadCount);
    }
}
