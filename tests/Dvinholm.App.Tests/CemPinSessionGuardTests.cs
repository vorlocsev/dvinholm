namespace Dvinholm.App.Tests;

public sealed class CemPinSessionGuardTests
{
    private static VolvoP2CemServiceResearchContext Context() =>
        new(2009, true, null, null, null,
            VolvoP2ServiceCredentialScope.ConfigurationService, true, null, null);

    private sealed class Provider(Func<CancellationToken, Task<CemPinProviderReceipt>> run)
        : ICemPinAuthorizedProvider
    {
        internal int Calls;
        public Task<CemPinProviderReceipt> TryRunAsync(CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            return run(token);
        }
    }

    private static Provider Immediate() => new(_ =>
        Task.FromResult(CemPinProviderReceipt.FinishedWithoutPin));

    [Fact]
    public async Task MissingConsentAndUnknownRevisionCannotInvokeProviderOrSendFrames()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var session = new J2534Session(adapter);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _));
        object context = new();
        var provider = Immediate();
        var service = new CemPinAttemptCoordinator(provider, _ => true);
        using var noConsent = CemPinSessionGuard.Capture(session, context, () => context, false);
        Assert.Equal(CemPinAttemptStatus.SessionInvalidated,
            (await service.TryRunAsync(Context(), sessionGuard: noConsent)).Status);
        using var guard = CemPinSessionGuard.Capture(session, context, () => context, true);
        service = new(provider, c => VolvoP2CemServiceCapabilityMatrix.Assess(
            VolvoP2CemServiceOperation.ReviewPinAcquisition, c).MayAcquirePin);
        Assert.Equal(CemPinAttemptStatus.Unsupported,
            (await service.TryRunAsync(Context(), sessionGuard: guard)).Status);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Fact]
    public void DisconnectedCaptureCannotBeRevivedByConnectOrReconnect()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var session = new J2534Session(adapter);
        object context = new();
        using var beforeConnect = CemPinSessionGuard.Capture(session, context, () => context, true);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _));
        Assert.False(beforeConnect.IsCurrent);
        using var connected = CemPinSessionGuard.Capture(session, context, () => context, true);
        Assert.True(connected.IsCurrent);
        session.Disconnect();
        Assert.False(connected.IsCurrent);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _));
        Assert.False(connected.IsCurrent);
        using var fresh = CemPinSessionGuard.Capture(session, context, () => context, true);
        Assert.True(fresh.IsCurrent);
        fresh.Dispose();
        Assert.False(fresh.IsCurrent);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Fact]
    public async Task ReceiveFailureFaultsCemTransportUntilExplicitReconnect()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var session = new J2534Session(adapter);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        object oldContext = new();
        using var oldGuard = CemPinSessionGuard.Capture(session,
            oldContext, () => oldContext, ownerConsent: true);
        Assert.True(oldGuard.IsCurrent);

        adapter.FailReads = true;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (session.IsConnected && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.False(session.IsConnected);
        Assert.False(oldGuard.IsCurrent);

        // Even if a buggy adapter stops reporting errors, neither the old
        // guard nor a newly captured guard may use this faulted transport.
        adapter.FailReads = false;
        object prematureContext = new();
        using var premature = CemPinSessionGuard.Capture(session,
            prematureContext, () => prematureContext, ownerConsent: true);
        Assert.False(premature.IsCurrent);
        Assert.Equal(0, adapter.TransmittedFrames);

        session.Disconnect();
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        Assert.False(oldGuard.IsCurrent);
        Assert.False(premature.IsCurrent);
        object recoveredContext = new();
        using var recovered = CemPinSessionGuard.Capture(session,
            recoveredContext, () => recoveredContext, ownerConsent: true);
        Assert.True(recovered.IsCurrent);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Fact]
    public async Task StaleGuardCannotFaultAnUnrelatedReconnectedCanBus()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var session = new J2534Session(adapter);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        object firstContext = new();
        using var firstGuard = CemPinSessionGuard.Capture(
            session, firstContext, () => firstContext, ownerConsent: true);
        Assert.True(firstGuard.IsCurrent);

        session.Disconnect();
        Assert.False(firstGuard.IsCurrent);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        Assert.True(session.IsConnected);
        // Deliberately leave the stale guard subscribed. An RX error in this
        // unrelated new connection is not evidence about the old bus lease.
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(Exception _) => reported.TrySetResult();
        session.ReadError += Observe;
        try
        {
            adapter.FailReads = true;
            await reported.Task.WaitAsync(TimeSpan.FromSeconds(5));
            adapter.FailReads = false;
            Assert.False(firstGuard.IsCurrent);
            Assert.True(session.IsConnected);
            Assert.Equal(0, adapter.TransmittedFrames);
        }
        finally
        {
            adapter.FailReads = false;
            session.ReadError -= Observe;
        }

        object secondContext = new();
        using var secondGuard = CemPinSessionGuard.Capture(
            session, secondContext, () => secondContext, ownerConsent: true);
        Assert.True(secondGuard.IsCurrent);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Fact]
    public async Task FreshGuardIgnoresStaleEpochAndOldLeaseIgnoresNewEpoch()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var session = new J2534Session(adapter);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        object first = new();
        using var oldGuard = CemPinSessionGuard.Capture(session, first, () => first, true);
        var oldEpoch = session.ConnectionEpoch;
        session.Disconnect();
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        var freshEpoch = session.ConnectionEpoch;
        Assert.NotEqual(oldEpoch, freshEpoch);
        object second = new();
        using var newGuard = CemPinSessionGuard.Capture(session, second, () => second, true);

        // Synthetic delayed notifications, no native or vehicle communication.
        var epochEvent = typeof(J2534Session).GetField("ReadErrorEpoch",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(epochEvent);
        var callbacks = (Action<long>?)epochEvent.GetValue(session);
        Assert.NotNull(callbacks);
        callbacks(oldEpoch);
        Assert.True(session.IsConnected);
        Assert.True(newGuard.IsCurrent);
        Assert.False(oldGuard.IsCurrent);
        Assert.Equal(0, adapter.TransmittedFrames);

        // A current-epoch error must fault the actual connected lease.
        callbacks(freshEpoch);
        Assert.False(session.IsConnected);
        Assert.False(newGuard.IsCurrent);
    }

    [Fact]
    public async Task ChangedEcuContextDiscardsEvenAnImmediateSyntheticCompletion()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var session = new J2534Session(adapter);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _));
        object context = new();
        var original = context;
        using var guard = CemPinSessionGuard.Capture(session, context, () => context, true);
        var provider = new Provider(_ =>
        {
            context = new();
            return Task.FromResult(CemPinProviderReceipt.FinishedWithoutPin);
        });
        var service = new CemPinAttemptCoordinator(provider, _ => true);
        var result = await service.TryRunAsync(Context(), sessionGuard: guard);
        Assert.Equal(CemPinAttemptStatus.SessionInvalidated, result.Status);
        Assert.False(result.HasPin);
        Assert.False(service.IsRunning);
        context = original;
        Assert.False(guard.IsCurrent);
    }

    [Fact]
    public async Task ReadFailureOrDisconnectCancelsRunningProviderBeforeGateIsReleased()
    {
        foreach (var readFailure in new[] { false, true })
        {
            using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
            using var session = new J2534Session(adapter);
            Assert.True(session.Connect("synthetic.dll", 500_000, out _));
            object context = new();
            using var guard = CemPinSessionGuard.Capture(session, context, () => context, true);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var provider = new Provider(async token =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return CemPinProviderReceipt.FinishedWithoutPin;
            });
            var service = new CemPinAttemptCoordinator(provider, _ => true);
            var running = service.TryRunAsync(Context(), sessionGuard: guard);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (readFailure) adapter.FailReads = true;
            else session.Disconnect();
            Assert.Equal(CemPinAttemptStatus.SessionInvalidated,
                (await running.WaitAsync(TimeSpan.FromSeconds(5))).Status);
            Assert.False(service.IsRunning);
            Assert.Equal(0, adapter.TransmittedFrames);
        }
    }

    [Fact]
    public async Task DeadlineCancelsCooperativeProviderAndAllowsFreshAttempt()
    {
        var provider = new Provider(async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return CemPinProviderReceipt.FinishedWithoutPin;
        });
        var service = new CemPinAttemptCoordinator(provider, _ => true);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var result = await service.TryRunAsync(Context(),
                sessionGuard: new(() => true), timeout: TimeSpan.FromMilliseconds(40))
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(CemPinAttemptStatus.TimedOut, result.Status);
            Assert.False(service.IsRunning);
        }
        Assert.Equal(2, provider.Calls);
        service = new(new Provider(_ => throw new TimeoutException("SYNTHETIC-PRIVATE")), _ => true);
        var transportTimeout = await service.TryRunAsync(Context(), sessionGuard: new(() => true));
        Assert.Equal(CemPinAttemptStatus.TimedOut, transportTimeout.Status);
        Assert.DoesNotContain("SYNTHETIC-PRIVATE", transportTimeout.ToString());
    }

    [Fact]
    public async Task CancellationKeepsSingleFlightUntilUncooperativeProviderTerminates()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<CemPinProviderReceipt>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new Provider(async token =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            entered.TrySetResult();
            return await release.Task;
        });
        var service = new CemPinAttemptCoordinator(provider, _ => true);
        using var cancellation = new CancellationTokenSource();
        var running = service.TryRunAsync(Context(), cancellation.Token, new(() => true));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(running.IsCompleted);
            Assert.True(service.IsRunning);
            Assert.Equal(CemPinAttemptStatus.AlreadyRunning,
                (await service.TryRunAsync(Context(), sessionGuard: new(() => true))).Status);
            Assert.Equal(1, provider.Calls);
        }
        finally { release.TrySetResult(CemPinProviderReceipt.FinishedWithoutPin); }
        Assert.Equal(CemPinAttemptStatus.Cancelled, (await running).Status);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task PrivateAuthorizationAndContextExceptionsAreRedactedAndGateRecovers()
    {
        const string privateData = "SYNTHETIC-PRIVATE-VIN-PIN";
        var provider = Immediate();
        var service = new CemPinAttemptCoordinator(provider,
            _ => throw new InvalidOperationException(privateData));
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var result = await service.TryRunAsync(Context(), sessionGuard: new(() => true));
            Assert.Equal(CemPinAttemptStatus.Failed, result.Status);
            Assert.DoesNotContain(privateData, result.ToString());
            Assert.False(service.IsRunning);
        }
        service = new(provider, _ => true);
        var invalid = await service.TryRunAsync(Context(),
            sessionGuard: new(() => throw new InvalidOperationException(privateData)));
        Assert.Equal(CemPinAttemptStatus.SessionInvalidated, invalid.Status);
        Assert.DoesNotContain(privateData, invalid.ToString());
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task MissingGuardAndInvalidDeadlineFailBeforeProviderInvocation()
    {
        var provider = Immediate();
        var service = new CemPinAttemptCoordinator(provider, _ => true);
        Assert.Equal(CemPinAttemptStatus.SessionInvalidated,
            (await service.TryRunAsync(Context())).Status);
        foreach (var timeout in new[] { TimeSpan.FromMilliseconds(-1), TimeSpan.FromHours(2) })
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                service.TryRunAsync(Context(), sessionGuard: new(() => true), timeout: timeout));
        Assert.Equal(0, provider.Calls);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public void DelayedOldReadFaultCannotPoisonReconnectedSessionAfterLeaseCheck()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var session = new J2534Session(adapter);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        var oldEpoch = session.ConnectionEpoch;
        object oldContext = new();
        using var oldGuard = CemPinSessionGuard.Capture(session, oldContext,
            () => oldContext, ownerConsent: true);
        Assert.True(oldGuard.IsCurrent);

        session.Disconnect();
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        object newContext = new();
        using var newGuard = CemPinSessionGuard.Capture(session, newContext,
            () => newContext, ownerConsent: true);
        Assert.True(newGuard.IsCurrent);

        // Models a receive callback paused immediately before faulting a bus.
        // Calling the epoch-bound transition after reconnect must be harmless.
        session.InvalidateTransportForReadError(oldEpoch);
        Assert.True(session.IsConnected);
        Assert.True(newGuard.IsCurrent);
        Assert.False(oldGuard.IsCurrent);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Fact]
    public void OlderDelayedReadFaultCannotEraseNewerTransportFault()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var session = new J2534Session(adapter);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        var oldEpoch = session.ConnectionEpoch;
        session.Disconnect();
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        var currentEpoch = session.ConnectionEpoch;
        Assert.True(currentEpoch > oldEpoch);

        session.InvalidateTransportForReadError(currentEpoch);
        Assert.False(session.IsConnected);
        session.InvalidateTransportForReadError(oldEpoch);
        Assert.False(session.IsConnected);
        object context = new();
        using var denied = CemPinSessionGuard.Capture(session, context,
            () => context, ownerConsent: true);
        Assert.False(denied.IsCurrent);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Fact]
    public void EpochScopedReceiveFaultRequiresExplicitReconnectForFreshLease()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var session = new J2534Session(adapter);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        var faultedEpoch = session.ConnectionEpoch;
        session.InvalidateTransportForReadError(faultedEpoch);

        object context = new();
        using var denied = CemPinSessionGuard.Capture(session, context,
            () => context, ownerConsent: true);
        Assert.False(session.IsConnected);
        Assert.False(denied.IsCurrent);
        Assert.Null(session.CaptureConnectedBusSnapshot());

        session.Disconnect();
        Assert.True(session.Connect("synthetic.dll", 500_000, out _, extended: true));
        using var fresh = CemPinSessionGuard.Capture(session, context,
            () => context, ownerConsent: true);
        Assert.True(session.IsConnected);
        Assert.True(fresh.IsCurrent);
        Assert.False(denied.IsCurrent);
        Assert.Equal(0, adapter.TransmittedFrames);
    }
}
