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
    public async Task ChangedEcuContextDiscardsEvenAnImmediateSyntheticCompletion()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var session = new J2534Session(adapter);
        Assert.True(session.Connect("synthetic.dll", 500_000, out _));
        object context = new();
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
}
