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
        internal bool RejectDispose;
        public override long GetTimestamp() => Ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            RejectTimer ? throw new InvalidOperationException("SYNTHETIC-PRIVATE-TIMER") :
                RejectDispose ? new FaultyDisposeTimer() : base.CreateTimer(callback, state, dueTime, period);
    }

    private sealed class FaultyDisposeTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() => throw new InvalidOperationException("SYNTHETIC-PRIVATE-DISPOSE");
        public ValueTask DisposeAsync() => ValueTask.FromException(
            new InvalidOperationException("SYNTHETIC-PRIVATE-DISPOSE"));
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
    public async Task DisposeCanWipePinWhileRevealCallbackIsPaused()
    {
        var owned = Fixture();
        using var secret = new CemPinSecret(owned, CemPinResultOrigin.AdapterUnattested);
        using var result = CemPinAttemptResult.Accepted(secret);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var matchedBefore = false;
        var erasedWhileCallbackPaused = false;
        var running = Task.Run(() => result.TryReveal(bytes =>
        {
            matchedBefore = bytes.SequenceEqual(Fixture());
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new IOException("Synthetic callback timeout");
            erasedWhileCallbackPaused = bytes.ToArray().All(b => b == 0);
        }));

        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            await Task.Run(secret.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.All(owned, b => Assert.Equal((byte)0, b));
            Assert.False(secret.IsAvailable);
        }
        finally { release.Set(); }

        Assert.False(await running.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(matchedBefore);
        Assert.True(erasedWhileCallbackPaused);
        Assert.False(result.HasPin);
    }

    [Fact]
    public async Task ExpiryCanWipePinWhileRevealCallbackIsPaused()
    {
        var clock = new Clock();
        var owned = Fixture();
        using var secret = new CemPinSecret(owned, CemPinResultOrigin.Synthetic, clock: clock);
        using var result = CemPinAttemptResult.Accepted(secret);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var erasedBeforeCallbackReturn = false;
        var running = Task.Run(() => result.TryReveal(bytes =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new IOException("Synthetic callback timeout");
            erasedBeforeCallbackReturn = bytes.ToArray().All(b => b == 0);
        }));

        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            clock.Ticks = TimeSpan.FromSeconds(31).Ticks;
            Assert.False(secret.IsAvailable);
            Assert.All(owned, b => Assert.Equal((byte)0, b));
        }
        finally { release.Set(); }

        Assert.False(await running.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(erasedBeforeCallbackReturn);
        Assert.False(result.HasPin);
    }

    [Fact]
    public void NestedRevealIsDeniedWithoutInvokingSecondCallback()
    {
        var owned = Fixture();
        using var secret = new CemPinSecret(owned, CemPinResultOrigin.AdapterUnattested);
        using var result = CemPinAttemptResult.Accepted(secret);
        var outerCalls = 0;
        var innerCalls = 0;
        var matched = false;

        var accepted = result.TryReveal(bytes =>
        {
            outerCalls++;
            matched = bytes.SequenceEqual(Fixture());
            Assert.False(result.TryReveal(_ => innerCalls++));
            Assert.False(secret.TryReveal(_ => innerCalls++));
        });

        Assert.True(accepted);
        Assert.True(matched);
        Assert.Equal(1, outerCalls);
        Assert.Equal(0, innerCalls);
        Assert.True(result.HasPin);
        // The guard applies only while the first callback is active.
        Assert.True(result.TryReveal(bytes =>
        {
            outerCalls++;
            matched = bytes.SequenceEqual(Fixture());
        }));
        Assert.True(matched);
        Assert.Equal(2, outerCalls);
        result.Dispose();
        Assert.All(owned, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public void NestedRevealRefusalDoesNotPreventExpiryAndRevocation()
    {
        var clock = new Clock();
        var owned = Fixture();
        using var secret = new CemPinSecret(owned, CemPinResultOrigin.Synthetic, clock: clock);
        using var result = CemPinAttemptResult.Accepted(secret);
        var nestedCalled = false;
        var accepted = result.TryReveal(_ =>
        {
            // The nested invocation must not call its callback even when
            // the timer has not yet expired.
            Assert.False(secret.TryReveal(_ => nestedCalled = true));
            clock.Ticks = TimeSpan.FromSeconds(31).Ticks;
        });
        Assert.False(accepted);
        Assert.False(nestedCalled);
        Assert.False(result.HasPin);
        Assert.All(owned, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public void CallbackReenteringDisposeCannotReportAnAvailablePin()
    {
        var owned = Fixture();
        using var secret = new CemPinSecret(owned, CemPinResultOrigin.AdapterUnattested);
        using var result = CemPinAttemptResult.Accepted(secret);
        var callbackCount = 0;
        var matched = false;
        var accepted = result.TryReveal(bytes =>
        {
            callbackCount++;
            matched = bytes.SequenceEqual(Fixture());
            secret.Dispose(); // Reentrant monitor acquisition by owner thread.
        });
        Assert.False(accepted);
        Assert.Equal(1, callbackCount);
        Assert.True(matched);
        Assert.All(owned, b => Assert.Equal((byte)0, b));
        Assert.False(result.HasPin);
        Assert.False(secret.IsAvailable);
        Assert.False(result.TryReveal(_ => callbackCount++));
        Assert.Equal(1, callbackCount);
    }

    [Fact]
    public void ExpiryReachedDuringRevealCallbackRevokesPayloadBeforeSuccess()
    {
        var clock = new Clock();
        var owned = Fixture();
        using var secret = new CemPinSecret(owned, CemPinResultOrigin.Synthetic,
            clock: clock);
        using var result = CemPinAttemptResult.Accepted(secret);
        var callbackCount = 0;
        var matched = false;
        var accepted = result.TryReveal(bytes =>
        {
            callbackCount++;
            matched = bytes.SequenceEqual(Fixture());
            clock.Ticks = TimeSpan.FromSeconds(31).Ticks;
        });
        Assert.False(accepted);
        Assert.Equal(1, callbackCount);
        Assert.True(matched);
        Assert.All(owned, b => Assert.Equal((byte)0, b));
        Assert.False(result.HasPin);
        Assert.False(result.TryReveal(_ => callbackCount++));
        Assert.Equal(1, callbackCount);
    }

    [Fact]
    public void FailedRevealCallbackWipesSecretAndSanitizesPrivateException()
    {
        var owned = Fixture();
        using var secret = new CemPinSecret(owned, CemPinResultOrigin.AdapterUnattested);
        using var result = CemPinAttemptResult.Accepted(secret);
        var callbacks = 0;
        const string privateMarker = "SYNTHETIC-PRIVATE-VIN-CREDENTIAL";

        var accepted = result.TryReveal(_ =>
        {
            callbacks++;
            throw new IOException(privateMarker);
        });

        Assert.False(accepted);
        Assert.Equal(1, callbacks);
        Assert.All(owned, b => Assert.Equal((byte)0, b));
        Assert.False(result.HasPin);
        Assert.False(secret.IsAvailable);
        Assert.False(result.TryReveal(_ => callbacks++));
        Assert.Equal(1, callbacks);
        Assert.DoesNotContain(privateMarker, result.ToString());
    }

    [Fact]
    public void FailedRevealCallbackAlsoRevokesRestoredPin()
    {
        var owned = Fixture();
        using var secret = new CemPinSecret(owned, CemPinResultOrigin.AdapterUnattested);
        using var restored = CemPinAttemptResult.Restored(secret);
        var callbacks = 0;
        Assert.False(restored.TryReveal(_ =>
        {
            callbacks++;
            throw new InvalidOperationException("SYNTHETIC-UI-CALLBACK-FAULT");
        }));
        Assert.Equal(1, callbacks);
        Assert.All(owned, b => Assert.Equal((byte)0, b));
        Assert.False(restored.HasPin);
        Assert.False(restored.TryReveal(_ => callbacks++));
        Assert.Equal(1, callbacks);
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
        var unexpectedReveal = false;
        Assert.False(result.TryReveal(_ => unexpectedReveal = true));
        Assert.False(unexpectedReveal, "Callback must never run: Disposed material was revealed.");
        Assert.False(secret.IsAvailable);
    }

    [Fact]
    public void RevokedCandidateLeaseErasesOwnedBytesAndHidesOrigin()
    {
        var bytes = Fixture();
        using var secret = new CemPinSecret(bytes, CemPinResultOrigin.AdapterUnattested);
        var current = true;
        using var result = CemPinAttemptResult.Accepted(secret, () => current);
        Assert.True(result.HasPin);
        current = false;
        Assert.Null(result.Origin);
        Assert.False(result.HasPin);
        var unexpectedReveal = false;
        Assert.False(result.TryReveal(_ => unexpectedReveal = true));
        Assert.False(unexpectedReveal, "Callback must never run: Revoked PIN was revealed.");
        Assert.All(bytes, value => Assert.Equal((byte)0, value));
        Assert.False(secret.IsAvailable);
    }

    [Fact]
    public void PrivateLeaseFailureErasesSecretWithoutLeakingExceptionText()
    {
        const string privateData = "SYNTHETIC-PRIVATE-ECU-IDENTIFIER";
        var bytes = Fixture();
        using var secret = new CemPinSecret(bytes, CemPinResultOrigin.Synthetic);
        using var result = CemPinAttemptResult.Accepted(secret,
            () => throw new InvalidOperationException(privateData));

        var unexpectedReveal = false;
        Assert.False(result.TryReveal(_ => unexpectedReveal = true));
        Assert.False(unexpectedReveal, "Callback must never run: Invalid PIN lease was revealed.");
        Assert.False(result.HasPin);
        Assert.All(bytes, value => Assert.Equal((byte)0, value));
        Assert.DoesNotContain(privateData, result.ToString());
    }

    [Fact]
    public void TimerErasesBytesWithoutAnyReaderPollingTheSecret()
    {
        var bytes = Fixture();
        using var secret = new CemPinSecret(bytes, CemPinResultOrigin.Synthetic,
            lifetime: TimeSpan.FromMilliseconds(25));
        Assert.True(SpinWait.SpinUntil(() => bytes.All(b => b == 0), TimeSpan.FromSeconds(5)));
        Assert.False(secret.IsAvailable);
        var unexpectedReveal = false;
        Assert.False(secret.TryReveal(_ => unexpectedReveal = true));
        Assert.False(unexpectedReveal, "Callback must never run: Expired material was revealed.");
    }

    [Fact]
    public void MonotonicExpiryRejectsRevealEvenBeforeTheTimerCallbackRuns()
    {
        var clock = new Clock();
        var bytes = Fixture();
        using var secret = new CemPinSecret(bytes, CemPinResultOrigin.Synthetic, clock: clock);
        clock.Ticks = TimeSpan.FromSeconds(31).Ticks;
        var unexpectedReveal = false;
        Assert.False(secret.TryReveal(_ => unexpectedReveal = true));
        Assert.False(unexpectedReveal, "Callback must never run: Expired material was revealed.");
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
    public void FaultyTimerDisposalCannotPreventSecretRevocationOrLeakError()
    {
        var clock = new Clock { RejectDispose = true };
        var owned = Fixture();
        using var secret = new CemPinSecret(owned, CemPinResultOrigin.AdapterUnattested, clock: clock);
        using var result = CemPinAttemptResult.Accepted(secret);
        Assert.True(result.HasPin);
        result.Dispose();
        result.Dispose();
        Assert.All(owned, b => Assert.Equal((byte)0, b));
        Assert.False(result.HasPin);
        Assert.False(secret.IsAvailable);
        Assert.DoesNotContain("SYNTHETIC-PRIVATE-DISPOSE", result.ToString());
        var called = false;
        Assert.False(result.TryReveal(_ => called = true));
        Assert.False(called);
    }

    [Fact]
    public void FaultyTimerDisposalCannotBreakMonotonicExpiry()
    {
        var clock = new Clock { RejectDispose = true };
        var owned = Fixture();
        using var secret = new CemPinSecret(owned, CemPinResultOrigin.Synthetic, clock: clock);
        using var result = CemPinAttemptResult.Accepted(secret);
        clock.Ticks = TimeSpan.FromSeconds(31).Ticks;
        Assert.False(result.HasPin);
        Assert.False(secret.IsAvailable);
        Assert.All(owned, b => Assert.Equal((byte)0, b));
        var called = false;
        Assert.False(result.TryReveal(_ => called = true));
        Assert.False(called);
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
        var unexpectedReveal = false;
        Assert.False(result.TryReveal(_ => unexpectedReveal = true));
        Assert.False(unexpectedReveal, "Callback must never run: Metadata-only result acquired a payload.");
    }
}
