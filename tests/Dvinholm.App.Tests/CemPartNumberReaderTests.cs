using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>Eight synthetic source-conformance checks; no physical support claim.</summary>
public sealed class CemPartNumberReaderTests
{
    private const uint ReplyId = 0x800003;
    private static CanFrame First(uint id = ReplyId) =>
        new(id, true, false, new byte[] { 0x80, 0, 0, 0, 0, 0x30, 0x68, 0x29 }, 0, 0);
    private static CanFrame Last(uint id = ReplyId) =>
        new(id, true, false, new byte[] { 0, 0x81, 0, 0, 0, 0, 0, 0 }, 0, 0);
    private static IEnumerable<CanFrame> Reply(CanFrame _) => new[] { First(), Last() };

    private sealed class Fixture : IDisposable
    {
        internal readonly VirtualJ2534Adapter Adapter;
        internal readonly J2534Session Session;
        internal readonly CemPinSessionGuard Guard;
        private object _context = new();
        internal Fixture(Func<CanFrame, IEnumerable<CanFrame>>? reply = null,
            uint baud = 500_000, bool extended = true, bool consent = true)
        {
            Adapter = new(reply ?? Reply);
            Session = new(Adapter);
            Assert.True(Session.Connect("synthetic.dll", baud, out _, extended));
            Guard = CemPinSessionGuard.Capture(Session, _context, () => Volatile.Read(ref _context), consent);
        }
        internal void ChangeContext() => Interlocked.Exchange(ref _context, new object());
        internal CemPartNumberObservation Read(bool approved = true, bool filterActive = false,
            TimeSpan timeout = default, CancellationToken token = default) =>
            CemPartNumberReader.Read(Session, Guard, approved, filterActive, timeout, token);
        public void Dispose() { Guard.Dispose(); Session.Dispose(); Adapter.Dispose(); }
    }

    [Fact]
    public void OneRequestReadsObservedPartNumberAndPreservesSharedDispatcher()
    {
        CanFrame? request = null;
        using var fixture = new Fixture(frame => { request = frame; return Reply(frame); });
        using var monitor = fixture.Session.Bus.Subscribe();
        var observed = fixture.Read();
        Assert.Equal(30_682_981u, observed.PartNumber);
        Assert.False(observed.PhysicalRevisionVerified);
        Assert.False(observed.PinRetrievalSupported);
        Assert.Contains("2d5de865", observed.Source);
        Assert.DoesNotContain("30682981", observed.ToString());
        Assert.True(fixture.Adapter.LastConnectExtended);
        Assert.Equal(500_000u, fixture.Session.ConnectedBaudRate);
        Assert.Equal(true, fixture.Session.ConnectedExtended);
        Assert.True(request.HasValue);
        Assert.Equal(0xFFFFEu, request.Value.ArbitrationId);
        Assert.True(request.Value.IsExtended);
        Assert.Equal(new byte[] { 0xCB, 0x50, 0xB9, 0xF0, 0, 0, 0, 0 }, request.Value.Data);
        Assert.Equal(1, fixture.Adapter.TransmittedFrames);
        Assert.Equal(0xFFFFu, fixture.Adapter.LastFilterMask);
        Assert.Equal(0, fixture.Adapter.ActiveFilterCount);
        Assert.True(monitor.TryRead(TimeSpan.FromSeconds(1), default, out _));
        Assert.True(monitor.TryRead(TimeSpan.FromSeconds(1), default, out _));
    }

    [Fact]
    public void ApprovalConsentChannelFormatSpeedAndFilterGuardsDenyBeforeTransmit()
    {
        using var valid = new Fixture();
        Assert.Throws<InvalidOperationException>(() => valid.Read(approved: false));
        Assert.Throws<InvalidOperationException>(() => valid.Read(filterActive: true));
        Assert.Equal(0, valid.Adapter.TransmittedFrames);
        foreach (var settings in new[] { (250_000u, true, true), (500_000u, false, true), (500_000u, true, false) })
        {
            using var fixture = new Fixture(baud: settings.Item1, extended: settings.Item2, consent: settings.Item3);
            Assert.Throws<InvalidOperationException>(() => fixture.Read());
            Assert.Equal(0, fixture.Adapter.TransmittedFrames);
            Assert.Equal(0, fixture.Adapter.ActiveFilterCount);
        }
        valid.Session.Disconnect();
        Assert.Null(valid.Session.ConnectedBaudRate);
        Assert.Null(valid.Session.ConnectedExtended);
        Assert.Throws<InvalidOperationException>(() => valid.Read());
        Assert.Equal(0, valid.Adapter.TransmittedFrames);
    }

    [Fact]
    public void MalformedFramesCannotSupplyOrMixPartNumberPairs()
    {
        using var fixture = new Fixture(_ => new[]
        {
            First(3) with { IsExtended = false },
            First(0x800004), First() with { Data = new byte[] { 0x80 } },
            First() with { Data = new byte[] { 0x80, 0, 0, 0, 0, 0xFA, 0, 0 } }, Last(),
            First() with { Data = new byte[] { 0x80, 0, 0, 0, 0, 0, 0, 0 } },
            Last() with { Data = new byte[8] }, // zero is not a canonical P/N
            First(), Last(0x900003), Last() with { Data = new byte[] { 0, 0xFF, 0, 0, 0, 0, 0, 0 } },
            Last(), // rejected first/last pair must not leave a reusable prefix
            First(), Last()
        });
        Assert.Equal(30_682_981u, fixture.Read().PartNumber);
        Assert.Equal(1, fixture.Adapter.TransmittedFrames);
        Assert.Equal(0, fixture.Adapter.ActiveFilterCount);
    }

    [Fact]
    public void TimeoutDoesNotReusePartialResponseAndLaterAttemptCanSucceed()
    {
        var attempts = 0;
        using var fixture = new Fixture(frame => ++attempts == 1 ? new[] { Last() } : Reply(frame));
        Assert.Throws<TimeoutException>(() => fixture.Read(timeout: TimeSpan.FromMilliseconds(40)));
        Assert.Equal(0, fixture.Adapter.ActiveFilterCount);
        Assert.Equal(30_682_981u, fixture.Read().PartNumber);
        Assert.Equal(2, fixture.Adapter.TransmittedFrames);
        Assert.Equal(0, fixture.Adapter.ActiveFilterCount);
    }

    [Fact]
    public async Task PreCancellationAndInFlightCancellationDoNotProduceAnObservation()
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = new Fixture(_ => { sent.TrySetResult(); return Array.Empty<CanFrame>(); });
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => fixture.Read(token: cancel.Token));
        Assert.Equal(0, fixture.Adapter.TransmittedFrames);
        using var during = new CancellationTokenSource();
        var read = Task.Factory.StartNew(() => fixture.Read(token: during.Token), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        during.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, fixture.Adapter.TransmittedFrames);
        Assert.Equal(0, fixture.Adapter.ActiveFilterCount);
    }

    [Fact]
    public async Task ContextChangesReadErrorsAndDisconnectsInvalidatePendingRead()
    {
        for (var mode = 0; mode < 3; mode++)
        {
            var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var fixture = new Fixture(_ => { sent.TrySetResult(); return Array.Empty<CanFrame>(); });
            var read = Task.Factory.StartNew(() => fixture.Read(), CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (mode == 0) fixture.ChangeContext();
            if (mode == 1) fixture.Adapter.FailReads = true;
            if (mode == 2) fixture.Session.Disconnect();
            await Assert.ThrowsAsync<InvalidOperationException>(() => read.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(1, fixture.Adapter.TransmittedFrames);
            Assert.Equal(0, fixture.Adapter.ActiveFilterCount);
        }
    }

    [Fact]
    public void FilterFailuresAndPrivateDriverExceptionsFailClosedAndRequireCleanup()
    {
        const string privateText = "SYNTHETIC-PRIVATE-VIN-PIN-PATH";
        using var rejected = new Fixture();
        rejected.Adapter.RejectMaskFilter = true;
        Assert.Throws<InvalidOperationException>(() => rejected.Read());
        Assert.Equal(0, rejected.Adapter.TransmittedFrames);
        var attempts = 0;
        using var throwing = new Fixture(frame => ++attempts == 1
            ? throw new InvalidOperationException(privateText) : Reply(frame));
        var error = Assert.Throws<InvalidOperationException>(() => throwing.Read());
        Assert.DoesNotContain(privateText, error.ToString());
        Assert.Equal(0, throwing.Adapter.ActiveFilterCount);
        Assert.Equal(30_682_981u, throwing.Read().PartNumber);

        using var cleanup = new Fixture();
        cleanup.Adapter.RejectStopFilter = true;
        Assert.Throws<CemPartNumberCleanupException>(() => cleanup.Read());
        Assert.False(cleanup.Session.IsConnected);
        Assert.Throws<InvalidOperationException>(() => cleanup.Session.Bus);
        Assert.Throws<InvalidOperationException>(() => cleanup.Read());
        Assert.Equal(1, cleanup.Adapter.TransmittedFrames);
        cleanup.Session.Disconnect();
        Assert.Equal(0, cleanup.Adapter.ActiveFilterCount);
    }

    [Fact]
    public unsafe void UiDefaultsRemainInactiveAndMaskUsesOnlyTheSourceDefinedIdBits()
    {
        var mask = CanHardwareFilter.CreateIdMask(0xFFFF, true);
        Assert.Equal(4u, mask.DataSize);
        Assert.Equal(CanDecoder.Can29BitId, mask.TxFlags);
        Assert.Equal(new byte[] { 0, 0, 0xFF, 0xFF }, new[] { mask.Data[0], mask.Data[1], mask.Data[2], mask.Data[3] });
        Assert.Throws<ArgumentOutOfRangeException>(() => CanHardwareFilter.CreateIdMask(0x800, false));
        using var stream = typeof(CemPartNumberReaderTests).Assembly.GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml");
        Assert.NotNull(stream);
        var document = XDocument.Load(stream);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var button = Assert.Single(document.Descendants(), e => (string?)e.Attribute(x + "Name") == "CemPartNumberReadButton");
        Assert.Equal("Collapsed", (string?)button.Attribute("Visibility"));
        Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
        Assert.Equal("ReadCemPartNumber_Click", (string?)button.Attribute("Click"));
        var mode = Assert.Single(document.Descendants(), e => (string?)e.Attribute(x + "Name") == "CanConnectionMode");
        Assert.Equal("0", (string?)mode.Attribute("SelectedIndex"));
        Assert.Equal("Collapsed", (string?)mode.Attribute("Visibility"));
        var pinPanel = Assert.Single(document.Descendants(), e => (string?)e.Attribute(x + "Name") == "CemPinLivePanel");
        Assert.Equal("Collapsed", (string?)pinPanel.Attribute("Visibility"));
        Assert.Equal("Dark", (string?)pinPanel.Attribute("RequestedTheme"));
        foreach (var name in new[] { "CemPinReadButton", "CemPinRevealButton" })
        {
            var pinButton = Assert.Single(document.Descendants(), e => (string?)e.Attribute(x + "Name") == name);
            Assert.Equal("False", (string?)pinButton.Attribute("IsEnabled"));
        }
        var pinText = Assert.Single(document.Descendants(), e => (string?)e.Attribute(x + "Name") == "CemPinResultText");
        Assert.Equal("No PIN result.", (string?)pinText.Attribute("Text"));
        Assert.Equal("False", (string?)pinText.Attribute("IsTextSelectionEnabled"));
    }
}
