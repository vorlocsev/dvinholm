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
            Assert.False(session.IsConnected);
        }
        finally
        {
            session.Disconnect();
        }
    }

    [Fact]
    public async Task ReceiveErrorPublishesOriginatingBusEpoch()
    {
        using var signalled = new ManualResetEventSlim();
        var adapter = new ReadErrorSessionAdapter();
        using var session = new J2534Session(adapter);
        long observedEpoch = -1;
        session.ReadErrorEpoch += epoch =>
        {
            observedEpoch = epoch;
            signalled.Set();
        };
        Assert.True(session.Connect("synthetic.dll", 500_000, out _));
        Assert.True(signalled.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(session.ConnectionEpoch, Volatile.Read(ref observedEpoch));
    }

    [Fact]
    public async Task DisconnectFromReadErrorCallbackDefersTeardownAndUnloadsAdapter()
    {
        var adapter = new ReadErrorSessionAdapter();
        using var session = new Dvinholm.App.J2534Session(adapter);
        session.ReadError += _ => session.Disconnect();

        Assert.True(session.Connect("virtual-adapter.dll", 500_000, out var error), error);

        var disconnected = await Task.Run(() => SpinWait.SpinUntil(
            () => !session.IsConnected,
            TimeSpan.FromSeconds(3)));

        Assert.True(disconnected, "Disconnect requested by the RX callback did not complete.");
        Assert.True(adapter.ReadErrorCount >= 1);
        Assert.Equal(2, adapter.UnloadCount);
        Assert.Null(session.AdapterVersion);
        Assert.Null(session.AdapterCapabilities);
    }

    [Fact]
    public void NativeReadFailureWithoutCemLeaseRequiresReconnection()
    {
        using var signalled = new ManualResetEventSlim();
        var adapter = new ReadErrorSessionAdapter();
        using var session = new J2534Session(adapter);
        session.ReadError += _ => signalled.Set();

        Assert.True(session.Connect("synthetic.dll", 500_000, out _));
        Assert.True(signalled.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(session.IsConnected);
        Assert.Null(session.CaptureConnectedBusSnapshot());

        // Later successful no-frame reads cannot re-arm the failed channel.
        Assert.False(session.IsConnected);
        session.Disconnect();
        Assert.True(session.Connect("synthetic.dll", 500_000, out _));
        Assert.True(session.IsConnected);
        Assert.NotNull(session.CaptureConnectedBusSnapshot());
    }

    [Fact]
    public void MalformedFrameNoticeDoesNotFaultNativeTransportWithoutCemLease()
    {
        using var signalled = new ManualResetEventSlim();
        var adapter = new ReadErrorSessionAdapter { MalformedFirstFrame = true };
        using var session = new J2534Session(adapter);
        string? errorText = null;
        session.ReadError += error =>
        {
            errorText = error.Message;
            signalled.Set();
        };

        Assert.True(session.Connect("synthetic.dll", 500_000, out _));
        Assert.True(signalled.Wait(TimeSpan.FromSeconds(5)));
        Assert.Contains("CAN decode error", errorText, StringComparison.Ordinal);
        Assert.True(session.IsConnected);
        Assert.NotNull(session.CaptureConnectedBusSnapshot());
    }

    private sealed class ReadErrorSessionAdapter : Dvinholm.App.IJ2534SessionAdapter
    {
        private int _readCount;
        private int _unloadCount;
        internal bool MalformedFirstFrame { get; init; }

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
            {
                if (MalformedFirstFrame)
                {
                    msg = new Dvinholm.App.J2534Native.PassthruMsg
                    {
                        ProtocolID = Dvinholm.App.J2534Native.ProtocolCan,
                        DataSize = 3
                    };
                    return true;
                }
                throw new InvalidOperationException("simulated adapter read failure");
            }

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
