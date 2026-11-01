namespace Dvinholm.App.Tests;

/// <summary>
/// Eight entirely synthetic CEM PIN lifecycle checks. Fake authorization in
/// these tests cannot unlock production CEM, J2534, PIN, or key operations.
/// </summary>
public sealed class CemPinAttemptCoordinatorTests
{
    private static VolvoP2CemServiceResearchContext Context() =>
        new(2009, true, null, "Synthetic mapping 01", "Synthetic service route 01",
            VolvoP2ServiceCredentialScope.ConfigurationService, true,
            "Synthetic bench 01", "Synthetic recovery 01");

    private sealed class FakeProvider(
        Func<CancellationToken, Task<CemPinProviderReceipt>> operation)
        : ICemPinAuthorizedProvider
    {
        internal int Calls;
        public Task<CemPinProviderReceipt> TryRunAsync(CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            return operation(token);
        }
    }

    private static CemPinAttemptCoordinator Synthetic(FakeProvider provider) =>
        new(provider, _ => true);

    [Fact]
    public async Task ProductionCoordinatorHasNoProviderAndCannotRun()
    {
        var service = new CemPinAttemptCoordinator();
        var result = await service.TryRunAsync(Context());
        Assert.Equal(CemPinAttemptStatus.Unsupported, result.Status);
        Assert.False(result.HasPin);
        Assert.False(result.MayWriteEcu);
        Assert.False(service.IsRunning);
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
    }

    [Fact]
    public async Task RealCapabilityMatrixBlocksInjectedFakeWithoutInvokingIt()
    {
        var fake = new FakeProvider(_ =>
            Task.FromResult(CemPinProviderReceipt.FinishedWithoutPin));
        var service = new CemPinAttemptCoordinator(fake, context =>
            VolvoP2CemServiceCapabilityMatrix.Assess(
                VolvoP2CemServiceOperation.ReviewPinAcquisition, context).MayAcquirePin);
        var result = await service.TryRunAsync(Context());
        Assert.Equal(CemPinAttemptStatus.Unsupported, result.Status);
        Assert.Equal(0, fake.Calls);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task NullResearchContextIsRejectedBeforeAnyProviderAction()
    {
        var fake = new FakeProvider(_ =>
            Task.FromResult(CemPinProviderReceipt.FinishedWithoutPin));
        var service = Synthetic(fake);
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await service.TryRunAsync(null!));
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task AlreadyCancelledRequestNeverStartsTheProvider()
    {
        var fake = new FakeProvider(_ =>
            Task.FromResult(CemPinProviderReceipt.FinishedWithoutPin));
        var service = Synthetic(fake);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await service.TryRunAsync(Context(), cts.Token);
        Assert.Equal(CemPinAttemptStatus.Cancelled, result.Status);
        Assert.Equal(0, fake.Calls);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task OneInFlightAttemptRejectsConcurrentAttemptAndReleasesGate()
    {
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeProvider(async ct =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return CemPinProviderReceipt.FinishedWithoutPin;
        });
        var service = Synthetic(fake);
        var first = service.TryRunAsync(Context());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(service.IsRunning);
        var second = await service.TryRunAsync(Context());
        Assert.Equal(CemPinAttemptStatus.AlreadyRunning, second.Status);
        Assert.Equal(1, fake.Calls);
        release.SetResult();
        Assert.Equal(CemPinAttemptStatus.FinishedWithoutPin,
            (await first).Status);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task CancellationDuringProviderReleasesGateForRetry()
    {
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeProvider(async ct =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return CemPinProviderReceipt.FinishedWithoutPin;
        });
        var service = Synthetic(fake);
        using var cts = new CancellationTokenSource();
        var running = service.TryRunAsync(Context(), cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        Assert.Equal(CemPinAttemptStatus.Cancelled, (await running).Status);
        Assert.False(service.IsRunning);
        using var nextCancellation = new CancellationTokenSource();
        nextCancellation.Cancel();
        Assert.Equal(CemPinAttemptStatus.Cancelled,
            (await service.TryRunAsync(Context(), nextCancellation.Token)).Status);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task PrivateProviderFailuresAreSanitizedAndDoNotPoisonNextAttempt()
    {
        const string secret = "PRIVATE-TEST-VIN-PIN-CREDENTIAL-9876";
        var fake = new FakeProvider(_ => throw new InvalidOperationException(secret));
        var service = Synthetic(fake);
        for (var n = 0; n < 2; n++)
        {
            var result = await service.TryRunAsync(Context());
            Assert.Equal(CemPinAttemptStatus.Failed, result.Status);
            Assert.DoesNotContain(secret, result.PublicMessage);
            Assert.DoesNotContain("InvalidOperationException", result.PublicMessage);
            Assert.False(service.IsRunning);
            Assert.False(result.HasPin);
        }
        Assert.Equal(2, fake.Calls);
    }

    [Fact]
    public async Task ProviderReceiptIsMetadataOnlyAndCannotClaimOrExposePin()
    {
        foreach (var receipt in Enum.GetValues<CemPinProviderReceipt>())
        {
            var fake = new FakeProvider(_ => Task.FromResult(receipt));
            var service = Synthetic(fake);
            var result = await service.TryRunAsync(Context());
            Assert.Equal(1, fake.Calls);
            Assert.False(result.HasPin);
            Assert.False(result.MayWriteEcu);
            Assert.False(service.IsRunning);
            Assert.DoesNotContain("2009", result.PublicMessage);
            Assert.DoesNotContain("Synthetic", result.PublicMessage);
            Assert.Equal(
                receipt == CemPinProviderReceipt.FinishedWithoutPin
                    ? CemPinAttemptStatus.FinishedWithoutPin
                    : CemPinAttemptStatus.Unsupported, result.Status);
        }
    }
}
