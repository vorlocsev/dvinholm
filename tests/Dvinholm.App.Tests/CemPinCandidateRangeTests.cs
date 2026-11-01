namespace Dvinholm.App.Tests;

/// <summary>Eight synthetic numeric checks; no ECU or PIN verification.</summary>
public sealed class CemPinCandidateRangeTests
{
    [Fact]
    public void EmitsSixPackedDecimalPairsWithExplicitLowPairFirstOrder()
    {
        var range = new CemPinCandidateRange(123456789012, 1);
        var buffer = new byte[6];
        Assert.True(range.TryNext(buffer));
        Assert.Equal(new byte[] { 0x12, 0x90, 0x78, 0x56, 0x34, 0x12 }, buffer);
        Assert.Equal(0UL, range.Remaining);
    }

    [Fact]
    public void DecimalCarryDoesNotGenerateHexDigitsAboveNine()
    {
        var range = new CemPinCandidateRange(98, 4);
        var buffer = new byte[6];
        foreach (var expected in new[]
        {
            new byte[] { 0x98, 0, 0, 0, 0, 0 },
            new byte[] { 0x99, 0, 0, 0, 0, 0 },
            new byte[] { 0, 0x01, 0, 0, 0, 0 },
            new byte[] { 0x01, 0x01, 0, 0, 0, 0 }
        })
        {
            Assert.True(range.TryNext(buffer));
            Assert.Equal(expected, buffer);
        }
    }

    [Fact]
    public void BoundedBatchHasNoDuplicatesOrInvalidPackedDecimalValues()
    {
        var range = new CemPinCandidateRange(9_950, 200);
        var buffer = new byte[6];
        var observed = new HashSet<ulong>();
        while (range.TryNext(buffer))
        {
            ulong ordinal = 0, weight = 1;
            foreach (var value in buffer)
            {
                Assert.InRange(value & 0x0F, 0, 9);
                Assert.InRange(value >> 4, 0, 9);
                ordinal += (ulong)((value >> 4) * 10 + (value & 0x0F)) * weight;
                weight *= 100;
            }
            Assert.True(observed.Add(ordinal));
        }
        Assert.Equal(Enumerable.Range(9_950, 200).Select(n => (ulong)n), observed.Order());
    }

    [Fact]
    public void LastCandidateIsReachableAndExhaustionLeavesBufferUntouched()
    {
        var range = new CemPinCandidateRange(CemPinCandidateRange.SearchSpaceSize - 1, 1);
        var buffer = new byte[6];
        Assert.True(range.TryNext(buffer));
        Assert.All(buffer, value => Assert.Equal((byte)0x99, value));
        buffer.AsSpan().Fill(0xCC);
        Assert.False(range.TryNext(buffer));
        Assert.All(buffer, value => Assert.Equal((byte)0xCC, value));
        Assert.Equal(0UL, range.Remaining);
    }

    [Fact]
    public void OrdinalsBeyondThirtyTwoBitsDoNotWrap()
    {
        var range = new CemPinCandidateRange((ulong)uint.MaxValue + 1, 1);
        var buffer = new byte[6];
        Assert.True(range.TryNext(buffer));
        Assert.Equal(new byte[] { 0x96, 0x72, 0x96, 0x94, 0x42, 0 }, buffer);
    }

    [Fact]
    public void InvalidOrOverflowingRangesAreRejectedBeforeIteration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CemPinCandidateRange(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CemPinCandidateRange(0, ulong.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CemPinCandidateRange(CemPinCandidateRange.SearchSpaceSize, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CemPinCandidateRange(CemPinCandidateRange.SearchSpaceSize - 1, 2));
        var fullSpace = new CemPinCandidateRange(0, CemPinCandidateRange.SearchSpaceSize);
        Assert.Equal(CemPinCandidateRange.SearchSpaceSize, fullSpace.Remaining);
    }

    [Fact]
    public void CancellationAndInvalidBuffersNeitherWriteNorAdvance()
    {
        var range = new CemPinCandidateRange(0, 2);
        var buffer = Enumerable.Repeat((byte)0xCC, 6).ToArray();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => range.TryNext(buffer, cancellation.Token));
        Assert.All(buffer, value => Assert.Equal((byte)0xCC, value));
        foreach (var length in new[] { 0, 5, 7 })
            Assert.Throws<ArgumentException>(() => range.TryNext(new byte[length]));
        Assert.Equal(2UL, range.Remaining);
        Assert.True(range.TryNext(buffer));
        Assert.All(buffer, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public async Task EnumerationCannotEnableProductionProviderOrAppearInResultText()
    {
        var range = new CemPinCandidateRange(123456789012, 1);
        var buffer = new byte[6];
        Assert.True(range.TryNext(buffer));
        Assert.DoesNotContain("123456789012", range.ToString());
        var context = new VolvoP2CemServiceResearchContext(2009, true, null, null,
            null, VolvoP2ServiceCredentialScope.ConfigurationService, true, null, null);
        var result = await new CemPinAttemptCoordinator().TryRunAsync(context,
            sessionGuard: new(() => true));
        Assert.Equal(CemPinAttemptStatus.Unsupported, result.Status);
        Assert.False(result.HasPin);
        Assert.False(result.MayWriteEcu);
        Assert.DoesNotContain("123456789012", result.ToString());
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(buffer);
        Assert.All(buffer, value => Assert.Equal((byte)0, value));
    }
}
