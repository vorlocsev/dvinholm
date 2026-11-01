namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, offline guards at the direct DID snapshot diff boundary.
/// These tests never contact J2534, decode a CEM or authorize ECU writes.
/// </summary>
public sealed class EcuSnapshotDiffCaptureMetadataTests
{
    [Fact]
    public void ValidUtcCaptureCanCompareOpaqueDidsWithoutClaimingEcuIdentity()
    {
        var before = Snapshot();
        var after = before with
        {
            CapturedAtUtc = before.CapturedAtUtc.AddSeconds(30),
            Values = [new EcuDidReadback(0xF187, "BB22")]
        };
        var result = EcuConfigurationSnapshotDiff.Compare(before, after);
        Assert.Equal(1, result.ChangedCount);
        Assert.Equal(before.CapturedAtUtc, result.OriginalCapturedAtUtc);
        Assert.Equal(after.CapturedAtUtc, result.CurrentCapturedAtUtc);
    }

    [Fact]
    public void DefaultCaptureTimestampFailsOnEitherSide()
    {
        var valid = Snapshot();
        RejectBoth(valid, valid with { CapturedAtUtc = default });
    }

    [Fact]
    public void NonUtcOffsetFailsOnEitherSideEvenAtSameAbsoluteInstant()
    {
        var valid = Snapshot();
        foreach (var offset in new[] { TimeSpan.FromHours(2), TimeSpan.FromHours(-5) })
            RejectBoth(valid, valid with
            {
                CapturedAtUtc = valid.CapturedAtUtc.ToOffset(offset)
            });
    }

    [Fact]
    public void StandardRequestCanIdOutsideElevenBitRangeFailsOnEitherSide()
    {
        var valid = Snapshot();
        RejectBoth(valid, valid with { RequestId = 0x800u });
        RejectBoth(valid, valid with { RequestId = uint.MaxValue });
    }

    [Fact]
    public void StandardResponseCanIdOutsideElevenBitRangeFailsOnEitherSide()
    {
        var valid = Snapshot();
        RejectBoth(valid, valid with { ResponseId = 0x800u });
        RejectBoth(valid, valid with { ResponseId = uint.MaxValue });
    }

    [Fact]
    public void ExtendedCanIdentifiersUseExactTwentyNineBitBounds()
    {
        var valid = Snapshot() with
        {
            RequestId = 0x1FFFFFFEu,
            ResponseId = 0x1FFFFFFFu,
            IsExtended = true
        };
        Assert.Equal(0, EcuConfigurationSnapshotDiff.Compare(valid, valid).ChangedCount);
        RejectBoth(valid, valid with { RequestId = 0x20000000u });
        RejectBoth(valid, valid with { ResponseId = 0x20000000u });
    }

    [Fact]
    public void IdenticalRequestAndResponseCanIdFailsEvenWhenDidsMatch()
    {
        var valid = Snapshot();
        RejectBoth(valid, valid with { ResponseId = valid.RequestId });
        RejectBoth(valid, valid with { RequestId = valid.ResponseId });
    }

    [Fact]
    public void MissingOrOverlongProfileMetadataCannotBecomeTrustedDiff()
    {
        var valid = Snapshot();
        foreach (var invalid in new[]
        {
            valid with { ProfileId = "" },
            valid with { ProfileId = "   " },
            valid with { ProfileId = new string('X', 121) },
            valid with { ProfileName = "" },
            valid with { ProfileName = new string('X', 161) }
        })
            RejectBoth(valid, invalid);
    }

    private static void RejectBoth(
        EcuConfigurationReadSnapshot valid,
        EcuConfigurationReadSnapshot invalid)
    {
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationSnapshotDiff.Compare(valid, invalid));
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationSnapshotDiff.Compare(invalid, valid));
    }

    private static EcuConfigurationReadSnapshot Snapshot() =>
        new("synthetic.p2.cem", "Offline research ECU",
            0x7E0, 0x7E8, false, false,
            new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero),
            [new EcuDidReadback(0xF187, "AA11")]);
}
