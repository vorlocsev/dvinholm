namespace Dvinholm.App.Tests;

public sealed class J2534SessionLifecycleTests
{
    [Theory]
    [InlineData("", 500_000u, "J2534 DLL path is empty.")]
    [InlineData("   ", 500_000u, "J2534 DLL path is empty.")]
    [InlineData("virtual-adapter.dll", 0u, "CAN baud rate must be greater than zero.")]
    public void InvalidConnectionInputsFailBeforeLoadingAdapter(
        string dllPath,
        uint baudRate,
        string expectedError)
    {
        var adapter = new FakeSessionAdapter();
        using var session = new Dvinholm.App.J2534Session(adapter);

        var connected = session.Connect(dllPath, baudRate, out var error);

        Assert.False(connected);
        Assert.Equal(expectedError, error);
        Assert.False(session.IsConnected);
        Assert.Null(session.AdapterVersion);
        Assert.Null(session.AdapterCapabilities);
        Assert.Equal(0, adapter.LoadCount);
        Assert.Equal(0, adapter.ConnectCount);
        Assert.Equal(0, adapter.ReadCount);
    }

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
    public void FailedConnectionCanBeRetriedSuccessfully()
    {
        var adapter = new FakeSessionAdapter { Failure = FailurePoint.Open };
        using var session = new Dvinholm.App.J2534Session(adapter);

        Assert.False(session.Connect("virtual-adapter.dll", 500_000, out var firstError));
        Assert.Contains("simulated open failure", firstError);
        Assert.False(session.IsConnected);
        Assert.Null(session.AdapterVersion);
        Assert.Null(session.AdapterCapabilities);

        // Model the device becoming available after the initial failed attempt.
        adapter.Failure = FailurePoint.None;

        Assert.True(session.Connect("virtual-adapter.dll", 500_000, out var retryError), retryError);
        Assert.True(session.IsConnected);
        Assert.NotNull(session.AdapterVersion);
        Assert.NotNull(session.AdapterCapabilities);
        Assert.Equal(2, adapter.LoadCount);
        Assert.Equal(1, adapter.ConnectCount);

        session.Disconnect();
        Assert.False(session.IsConnected);
        Assert.Null(session.AdapterVersion);
        Assert.Null(session.AdapterCapabilities);
    }

    [Fact]
    public void UnexpectedAdapterExceptionDoesNotExposeLocalPath()
    {
        const string localPath = @"C:\Users\DiagnosticUser\AppData\Local\Temp\vendor-driver.dll";
        var adapter = new FakeSessionAdapter
        {
            Failure = FailurePoint.ReadVersion,
            ReadVersionExceptionMessage = "Failed to inspect " + localPath
        };
        using var session = new Dvinholm.App.J2534Session(adapter);

        var connected = session.Connect("virtual-adapter.dll", 500_000, out var error);

        Assert.False(connected);
        Assert.Contains("unexpected adapter error", error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(localPath, error, StringComparison.OrdinalIgnoreCase);
        Assert.False(session.IsConnected);
    }

    [Fact]
    public void UnexpectedReadExceptionDoesNotExposeLocalPath()
    {
        const string localPath = @"C:\Users\DiagnosticUser\AppData\Local\Temp\vendor-driver.dll";
        using var readErrorReported = new ManualResetEventSlim();
        var adapter = new FakeSessionAdapter
        {
            ReadExceptionMessage = "Native read failed at " + localPath
        };
        using var session = new Dvinholm.App.J2534Session(adapter);
        string? reportedError = null;
        session.ReadError += error =>
        {
            reportedError = error.Message;
            readErrorReported.Set();
        };

        Assert.True(session.Connect("virtual-adapter.dll", 500_000, out var connectError), connectError);
        Assert.True(readErrorReported.Wait(TimeSpan.FromSeconds(3)),
            "The unexpected read exception was not reported.");

        Assert.Equal("J2534 read failed due to an unexpected adapter error.", reportedError);
        Assert.DoesNotContain(localPath, reportedError, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("J2534 adapter cleanup failed due to an unexpected driver error.", error);
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
    public void DisconnectClearsMetadataWhenAdapterUnloadThrowsAndCanRetry()
    {
        var adapter = new FakeSessionAdapter { ThrowOnUnloadCall = 2 };
        using var session = new Dvinholm.App.J2534Session(adapter);

        Assert.True(session.Connect("virtual-adapter.dll", 500_000, out var connectError), connectError);
        Assert.NotNull(session.AdapterVersion);
        Assert.NotNull(session.AdapterCapabilities);

        var error = Assert.Throws<InvalidOperationException>(() => session.Disconnect());

        Assert.Contains("J2534 adapter cleanup failed due to an unexpected driver error.", error.Message);
        Assert.False(session.IsConnected);
        Assert.Null(session.AdapterVersion);
        Assert.Null(session.AdapterCapabilities);

        // A failed native unload can be retried without reconnecting or
        // retaining stale connection metadata.
        session.Disconnect();
        Assert.Equal(3, adapter.UnloadCount);
    }

    [Fact]
    public void DisposeCanRetryAfterAdapterUnloadFailure()
    {
        var adapter = new FakeSessionAdapter { ThrowOnUnloadCall = 2 };
        var session = new Dvinholm.App.J2534Session(adapter);

        Assert.True(session.Connect("virtual-adapter.dll", 500_000, out var connectError), connectError);
        Assert.NotNull(session.AdapterVersion);
        Assert.NotNull(session.AdapterCapabilities);

        var error = Assert.Throws<InvalidOperationException>(() => session.Dispose());

        Assert.Contains("J2534 adapter cleanup failed due to an unexpected driver error.", error.Message);
        Assert.False(session.IsConnected);
        Assert.Null(session.AdapterVersion);
        Assert.Null(session.AdapterCapabilities);

        // Dispose must not mark the session permanently disposed until native
        // teardown succeeds; a second call should retry the unload.
        session.Dispose();
        Assert.Equal(3, adapter.UnloadCount);
        Assert.Throws<ObjectDisposedException>(() =>
            session.Connect("again.dll", 500_000, out _));
        session.Dispose();
    }

    [Fact]
    public async Task DisconnectFromReadErrorCallbackDefersAdapterUnloadUntilCallbackReturns()
    {
        using var callbackEntered = new ManualResetEventSlim();
        using var allowCallbackToFinish = new ManualResetEventSlim();
        var adapter = new FakeSessionAdapter { FailFirstRead = true };
        using var session = new Dvinholm.App.J2534Session(adapter);

        session.ReadError += _ =>
        {
            session.Disconnect();
            callbackEntered.Set();
            allowCallbackToFinish.Wait();
        };

        Assert.True(session.Connect("virtual-adapter.dll", 500_000, out var connectError), connectError);
        try
        {
            Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(2)),
                "The session did not report the simulated adapter read error.");

            // Disconnect is deferred off the native read worker and must not
            // unload the adapter while the callback still owns that worker.
            Assert.Equal(1, adapter.UnloadCount);
            Assert.True(session.IsConnected);
        }
        finally
        {
            allowCallbackToFinish.Set();
        }

        Assert.True(SpinWait.SpinUntil(
                () => !session.IsConnected && adapter.UnloadCount == 2,
                TimeSpan.FromSeconds(3)),
            "Deferred disconnect did not finish after the read-error callback returned.");
        Assert.False(session.IsConnected);
    }

    [Fact]
    public async Task DeferredDisconnectFromOldReadWorkerDoesNotDisconnectReconnectedSession()
    {
        using var callbackEntered = new ManualResetEventSlim();
        using var allowCallbackToFinish = new ManualResetEventSlim();
        var adapter = new FakeSessionAdapter { FailFirstRead = true };
        using var session = new Dvinholm.App.J2534Session(adapter);
        session.ReadError += _ =>
        {
            session.Disconnect();
            callbackEntered.Set();
            allowCallbackToFinish.Wait();
        };

        Assert.True(session.Connect("first.dll", 500_000, out var firstError), firstError);
        try
        {
            Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(3)),
                "The initial read-error callback did not run.");

            // The old read worker has already queued a deferred disconnect.
            // Reconnect while the callback is still active, then release the
            // worker so the lifecycle operations can finish in either order.
            var reconnectTask = Task.Run(() =>
                session.Connect("second.dll", 250_000, out var error)
                    ? string.Empty
                    : error);

            await Task.Delay(100);
            allowCallbackToFinish.Set();

            var reconnectError = await reconnectTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(string.Empty, reconnectError);
        }
        finally
        {
            allowCallbackToFinish.Set();
        }

        Assert.True(SpinWait.SpinUntil(
                () => session.IsConnected && adapter.ConnectCount == 2,
                TimeSpan.FromSeconds(3)),
            "Reconnect did not establish the replacement session.");
        var reconnectedBus = session.Bus;

        // Any deferred teardown associated with the old bus must not affect
        // the replacement session after reconnect has completed.
        await Task.Delay(200);
        Assert.True(session.IsConnected);
        Assert.Same(reconnectedBus, session.Bus);
        Assert.Equal(2, adapter.ConnectCount);
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
        private int _readErrorIssued;
        internal FailurePoint Failure { get; set; }
        internal string? ReadVersionExceptionMessage { get; init; }
        internal string? ReadExceptionMessage { get; init; }
        internal bool FailFirstRead { get; init; }
        internal ManualResetEventSlim? ReadEntered { get; init; }
        internal ManualResetEventSlim? AllowReadToFinish { get; init; }
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
                throw new InvalidOperationException(ReadVersionExceptionMessage ?? "simulated version query failure");
            return new("firmware", "dll", "api");
        }
        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            Interlocked.Increment(ref _readCount);
            msg = default;
            if (ReadExceptionMessage is not null)
                throw new InvalidOperationException(ReadExceptionMessage);
            if (FailFirstRead && Interlocked.Exchange(ref _readErrorIssued, 1) == 0)
            {
                ReadEntered?.Set();
                AllowReadToFinish?.Wait(TimeSpan.FromSeconds(5));
                error = "simulated adapter read failure";
                return false;
            }

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
