#if DVINHOLM_DEVELOPMENT_FEATURES
namespace Dvinholm.App.Tests;

/// <summary>Synthetic transport conformance only; no vehicle acceptance.</summary>
public sealed class ExperimentalCemPinCanProviderTests
{
    private static CanFrame Frame(params byte[] bytes) => new(0x800003, true, false, bytes, 0, 0);
    private static CanFrame Identity() => Frame(0x50, 0xB9, 0, 0, 0x30, 0x68, 0x29, 0x81);
    private static CanFrame Ack(byte status = 0) => Frame(0x50, 0xB9, status, 0, 0, 0, 0, 0);
    private sealed class Fixture : IDisposable
    {
        internal readonly VirtualJ2534Adapter Adapter;
        internal readonly J2534Session Session;
        internal readonly CemPinSessionGuard Guard;
        private object _context = new();
        internal Fixture(Func<CanFrame, IEnumerable<CanFrame>> reply, bool extended = true, bool consent = true)
        {
            Adapter = new(reply);
            Session = new(Adapter);
            Assert.True(Session.Connect("synthetic.dll", 500000, out _, extended));
            Guard = CemPinSessionGuard.Capture(Session, _context, () => Volatile.Read(ref _context), consent);
        }
        internal void Invalidate() => Interlocked.Exchange(ref _context, new object());
        internal ExperimentalCemPinCanProvider Provider(uint pn = 30682981, ulong first = 0, ulong count = 10, bool approved = true) =>
            new(Session, Guard, pn, first, count, approved, CemPinResultOrigin.Synthetic, TimeSpan.FromMilliseconds(200));
        internal Task<CemPinAttemptResult> Run(ExperimentalCemPinCanProvider provider, CancellationToken token = default) =>
            CemPinAttemptCoordinator.ForSourceExperiment(provider).TryRunAsync(
                new(null, false, null, null, null, VolvoP2ServiceCredentialScope.Unspecified, false, null, null), token, Guard);
        public void Dispose() { Guard.Dispose(); Session.Dispose(); Adapter.Dispose(); }
    }

    [Fact]
    public async Task AcceptedCandidateRequiresSeparateConfirmationAndExplicitReveal()
    {
        var requests = new List<CanFrame>();
        var target = new byte[] { 0x56, 0x90, 0x34, 0x05, 0x12, 0x78 };
        using var f = new Fixture(request =>
        {
            requests.Add(request);
            return new[] { request.Data[1] == 0x88 ? Identity() : Ack(request.Data.AsSpan(2).SequenceEqual(target) ? (byte)0 : (byte)1) };
        });
        using var result = await f.Run(f.Provider(first: 123456789000));
        Assert.Equal(CemPinAttemptStatus.CandidateAccepted, result.Status);
        Assert.True(result.HasPin);
        Assert.Equal(CemPinResultOrigin.Synthetic, result.Origin);
        Assert.False(result.PhysicalAcceptanceVerified);
        Assert.False(result.MayWriteEcu);
        Assert.True(result.TryReveal(bytes => Assert.Equal(target, bytes.ToArray())));
        Assert.Equal(8, requests.Count);
        Assert.Equal(requests[^2].Data, requests[^1].Data);
        Assert.All(requests, r => { Assert.Equal(0xFFFFEu, r.ArbitrationId); Assert.True(r.IsExtended);
            Assert.Equal(0x50, r.Data[0]); Assert.Contains(r.Data[1], new byte[] { 0x88, 0xBE }); });
        Assert.Equal(0, f.Adapter.ActiveFilterCount);
    }

    [Fact]
    public async Task UnknownHardwareApprovalAndChannelDenyWithoutTransmission()
    {
        foreach (var pn in new uint[] { 0, 30682982, 12345678 })
        {
            using var f = new Fixture(_ => new[] { Identity() });
            using var result = await f.Run(f.Provider(pn: pn));
            Assert.Equal(CemPinAttemptStatus.Unsupported, result.Status);
            Assert.Equal(0, f.Adapter.TransmittedFrames);
        }
        foreach (var settings in new[] { (true, true, false), (false, true, true), (true, false, true) })
        {
            using var f = new Fixture(_ => new[] { Identity() }, settings.Item1, settings.Item2);
            using var result = await f.Run(f.Provider(approved: settings.Item3));
            Assert.False(result.HasPin);
            Assert.Equal(0, f.Adapter.TransmittedFrames);
        }
    }

    [Fact]
    public async Task UnavailableOrMismatchedContextSendsNoCandidates()
    {
        foreach (var reply in new[] { Array.Empty<CanFrame>(), new[] { Identity() with { Data = new byte[8] } },
            new[] { Identity() with { Data = new byte[] { 0x50, 0xB9, 0, 0, 0x30, 0x68, 0x29, 0x82 } } } })
        {
            using var f = new Fixture(_ => reply);
            using var result = await f.Run(f.Provider());
            Assert.False(result.HasPin);
            Assert.Equal(1, f.Adapter.TransmittedFrames);
            Assert.Equal(0, f.Adapter.ActiveFilterCount);
        }
    }

    [Fact]
    public async Task MalformedChangedOrUnconfirmedAcknowledgementStopsImmediately()
    {
        foreach (var mode in Enumerable.Range(0, 4))
        {
            var attempts = 0;
            using var f = new Fixture(r =>
            {
                if (r.Data[1] == 0x88) return new[] { Identity() };
                attempts++;
                return mode switch
                {
                    0 => new[] { Ack() with { Data = new byte[] { 0x50, 0xB9, 0, 1, 0, 0, 0, 0 } } },
                    1 => new[] { Ack(attempts == 1 ? (byte)1 : (byte)2) },
                    2 => new[] { Ack(attempts == 1 ? (byte)0 : (byte)1) },
                    _ => Array.Empty<CanFrame>()
                };
            });
            using var result = await f.Run(f.Provider());
            Assert.False(result.HasPin);
            Assert.InRange(attempts, 1, 2);
            Assert.Null(f.Provider().TakeCandidate());
            Assert.Equal(0, f.Adapter.ActiveFilterCount);
        }
    }

    [Fact]
    public async Task CancellationAndContextLossDiscardPendingOperation()
    {
        foreach (var cancel in new[] { true, false })
        {
            var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var f = new Fixture(r =>
            {
                if (r.Data[1] == 0x88) return new[] { Identity() };
                sent.TrySetResult();
                return Array.Empty<CanFrame>();
            });
            using var cts = new CancellationTokenSource();
            var operation = f.Run(f.Provider(), cts.Token);
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancel) cts.Cancel(); else f.Invalidate();
            using var result = await operation;
            Assert.Equal(cancel ? CemPinAttemptStatus.Cancelled : CemPinAttemptStatus.SessionInvalidated, result.Status);
            Assert.False(result.HasPin);
            Assert.Equal(0, f.Adapter.ActiveFilterCount);
        }
    }

    [Fact]
    public async Task ExhaustionAndBatchBoundsCannotProduceMaterial()
    {
        using var f = new Fixture(r => new[] { r.Data[1] == 0x88 ? Identity() : Ack(1) });
        using var result = await f.Run(f.Provider(first: 100, count: 3));
        Assert.Equal(CemPinAttemptStatus.FinishedWithoutPin, result.Status);
        Assert.False(result.HasPin);
        Assert.Equal(4, f.Adapter.TransmittedFrames);
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Provider(count: 10001));
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Provider(first: CemPinCandidateRange.SearchSpaceSize - 1, count: 2));
    }

    [Fact]
    public async Task CleanupFailureDiscardsAcceptedMaterialAndInvalidatesTransport()
    {
        using var f = new Fixture(r => new[] { r.Data[1] == 0x88 ? Identity() : Ack() });
        f.Adapter.RejectStopFilter = true;
        var provider = f.Provider();
        using var result = await f.Run(provider);
        Assert.Equal(CemPinAttemptStatus.SessionInvalidated, result.Status);
        Assert.False(result.HasPin);
        Assert.Null(provider.TakeCandidate());
        Assert.False(f.Session.IsConnected);
    }

    [Fact]
    public async Task PrivateTransportErrorsCannotEnterMetadata()
    {
        using var f = new Fixture(_ => throw new InvalidOperationException("SYNTHETIC-PRIVATE-CANDIDATE"));
        using var result = await f.Run(f.Provider());
        Assert.False(result.HasPin);
        Assert.DoesNotContain("SYNTHETIC-PRIVATE", result.ToString());
        Assert.Equal(0, f.Adapter.ActiveFilterCount);
    }
}
#endif
