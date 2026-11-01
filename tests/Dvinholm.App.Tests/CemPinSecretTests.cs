using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>Eight synthetic confidential-result checks; no vehicle PIN fixture.</summary>
public sealed class CemPinSecretTests
{
    private static byte[] Fixture() => new byte[] { 0x12, 0x34, 0x56, 0x78, 0x90, 0x12 };
    private sealed class Clock : TimeProvider
    {
        internal long Ticks;
        internal bool RejectTimer;
        public override long GetTimestamp() => Ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            RejectTimer ? throw new InvalidOperationException("SYNTHETIC-PRIVATE-TIMER") : base.CreateTimer(callback, state, dueTime, period);
    }

    [Fact]
    public void MaterialIsRedactedFromStringsAndDefaultJson()
    {
        using var secret = new CemPinSecret(Fixture(), CemPinResultOrigin.Synthetic);
        using var result = CemPinAttemptResult.Accepted(secret);
        Assert.True(result.HasPin);
        Assert.Equal(CemPinResultOrigin.Synthetic, result.Origin);
        Assert.False(result.PhysicalAcceptanceVerified);
        Assert.False(result.MayWriteEcu);
        foreach (var text in new[] { secret.ToString(), result.ToString(), JsonSerializer.Serialize(secret), JsonSerializer.Serialize(result) })
            Assert.DoesNotContain("123456789012", text);
    }

    [Fact]
    public void ExplicitRevealIsTheOnlyPayloadAccessAndDoesNotConsumeMetadata()
    {
        using var secret = new CemPinSecret(Fixture(), CemPinResultOrigin.Synthetic);
        var calls = 0;
        using var result = CemPinAttemptResult.Accepted(secret);
        Assert.Equal(0, calls);
        Assert.True(result.TryReveal(bytes => { calls++; Assert.True(bytes.SequenceEqual(Fixture())); }));
        Assert.Equal(1, calls);
        Assert.True(result.HasPin);
    }

    [Fact]
    public void DisposalErasesOwnedBytesAndPreventsFurtherReveal()
    {
        var bytes = Fixture();
        var secret = new CemPinSecret(bytes, CemPinResultOrigin.AdapterUnattested);
        var result = CemPinAttemptResult.Accepted(secret);
        result.Dispose();
        result.Dispose();
        Assert.All(bytes, value => Assert.Equal((byte)0, value));
        Assert.False(result.HasPin);
        Assert.False(result.TryReveal(_ => Assert.Fail("Disposed material was revealed.")));
        Assert.False(secret.IsAvailable);
    }

    [Fact]
    public void TimerErasesBytesWithoutAnyReaderPollingTheSecret()
    {
        var bytes = Fixture();
        using var secret = new CemPinSecret(bytes, CemPinResultOrigin.Synthetic,
            lifetime: TimeSpan.FromMilliseconds(25));
        Assert.True(SpinWait.SpinUntil(() => bytes.All(b => b == 0), TimeSpan.FromSeconds(5)));
        Assert.False(secret.IsAvailable);
        Assert.False(secret.TryReveal(_ => Assert.Fail("Expired material was revealed.")));
    }

    [Fact]
    public void MonotonicExpiryRejectsRevealEvenBeforeTheTimerCallbackRuns()
    {
        var clock = new Clock();
        var bytes = Fixture();
        using var secret = new CemPinSecret(bytes, CemPinResultOrigin.Synthetic, clock: clock);
        clock.Ticks = TimeSpan.FromSeconds(31).Ticks;
        Assert.False(secret.TryReveal(_ => Assert.Fail("Expired material was revealed.")));
        Assert.All(bytes, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void InvalidShapeOriginAndLifetimeCannotCreateResultMaterial()
    {
        Assert.Throws<ArgumentException>(() => new CemPinSecret(new byte[5], CemPinResultOrigin.Synthetic));
        Assert.Throws<ArgumentException>(() => new CemPinSecret(new byte[] { 0xFA, 0, 0, 0, 0, 0 }, CemPinResultOrigin.Synthetic));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CemPinSecret(Fixture(), (CemPinResultOrigin)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CemPinSecret(Fixture(), CemPinResultOrigin.Synthetic, TimeSpan.FromSeconds(31)));
    }

    [Fact]
    public void ExpiryInfrastructureFailureErasesTransferredBytesAndRedactsItsError()
    {
        var bytes = Fixture();
        var error = Assert.Throws<InvalidOperationException>(() =>
            new CemPinSecret(bytes, CemPinResultOrigin.Synthetic, clock: new Clock { RejectTimer = true }));
        Assert.DoesNotContain("SYNTHETIC-PRIVATE-TIMER", error.ToString());
        Assert.All(bytes, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public async Task MetadataReceiptsStillCannotCreateASecret()
    {
        var result = await new CemPinAttemptCoordinator().TryRunAsync(
            new VolvoP2CemServiceResearchContext(null, false, null, null, null,
                VolvoP2ServiceCredentialScope.Unspecified, false, null, null), sessionGuard: new(() => true));
        Assert.Equal(CemPinAttemptStatus.Unsupported, result.Status);
        Assert.False(result.HasPin);
        Assert.False(result.TryReveal(_ => Assert.Fail("Metadata-only result acquired a payload.")));
    }
}
