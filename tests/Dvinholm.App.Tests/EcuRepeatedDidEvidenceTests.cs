namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, deterministic offline cases: no real Volvo P2/CEM
/// address, firmware, PIN, credential, J2534 or vehicle write is exercised.
/// </summary>
public sealed class EcuRepeatedDidEvidenceTests
{
    [Fact]
    public void ReportsChangedAndUnchangedDidsWithoutAssumingCemIdentity()
    {
        var report = Compare(
            DocAt(5, (0xF187, "AA"), (0xF189, "BB")),
            DocAt(6, (0xF187, "AA"), (0xF189, "BC")));
        Assert.Equal(2, report.Observations.Count);
        Assert.Equal(1, report.SameValueCount);
        Assert.Equal(1, report.ChangedValueCount);
        Assert.Equal(0, report.LengthChangedCount);
        Assert.Contains("DID F187: earlier 1 byte(s), later 1 byte(s); value unchanged", report.Summary);
        Assert.Contains("DID F189: earlier 1 byte(s), later 1 byte(s); value changed", report.Summary);
        AssertNoEcuPermissions(report);
    }

    [Fact]
    public void ClassifiesChangedByteLengthWithoutRevealingActualData()
    {
        var report = Compare(
            DocAt(5, (0xF187, "01")),
            DocAt(6, (0xF187, "010203")));
        var did = Assert.Single(report.Observations);
        Assert.Equal(1, did.EarlierBytes);
        Assert.Equal(3, did.LaterBytes);
        Assert.True(did.ValueChanged);
        Assert.True(did.LengthChanged);
        Assert.Equal(1, report.LengthChangedCount);
        Assert.DoesNotContain("010203", report.Summary);
        AssertNoEcuPermissions(report);
    }

    [Fact]
    public void SortsDidRowsDeterministicallyFromEitherFileOrder()
    {
        var report = Compare(
            DocAt(5, (0xF189, "BB"), (0xF187, "AA")),
            DocAt(6, (0xF187, "AA"), (0xF189, "BB")));
        Assert.Equal(new ushort[] { 0xF187, 0xF189 },
            report.Observations.Select(x => x.DataIdentifier));
        Assert.Equal(2, report.SameValueCount);
        Assert.Equal(0, report.ChangedValueCount);
        AssertNoEcuPermissions(report);
    }

    [Fact]
    public void RequiresStrictlyIncreasingUtcCaptureTimes()
    {
        var earlier = DocAt(5, (0xF187, "AA"));
        foreach (var after in new[]
        {
            DocAt(5, (0xF187, "AB")),
            DocAt(4, (0xF187, "AB"))
        })
            Assert.Throws<InvalidDataException>(() => Compare(earlier, after));
        Assert.Throws<InvalidDataException>(() =>
            Compare(earlier, DocAt(6, (0xF187, "AA")) with
            {
                CapturedAtUtc = new DateTimeOffset(2026, 10, 27, 6, 0, 0,
                    TimeSpan.FromHours(3))
            }));
    }

    [Fact]
    public void RejectsMismatchedProfileCanAndFraming()
    {
        var before = DocAt(5, (0xF187, "AA"));
        var after = DocAt(6, (0xF187, "AA"));
        foreach (var malformed in new[]
        {
            after with { ProfileId = "other-profile" },
            after with { RequestCanId = "7E1" },
            after with { ResponseCanId = "7E9" },
            after with { IsExtended = true, RequestCanId = "000007E0", ResponseCanId = "000007E8" }
        })
            Assert.Throws<InvalidDataException>(() => Compare(before, malformed));
    }

    [Fact]
    public void RejectsIncompleteDuplicateAndInvalidDidSets()
    {
        var before = DocAt(5, (0xF187, "AA"), (0xF189, "BB"));
        foreach (var after in new[]
        {
            DocAt(6, (0xF187, "AA")),
            DocAt(6, (0xF187, "AA"), (0xF187, "BB")),
            DocAt(6, (0xF187, "AA"), (0xF189, "XYZ")),
            DocAt(6, (0xF187, "AA"), (0xF189, "B"))
        })
            Assert.Throws<InvalidDataException>(() => Compare(before, after));
    }

    [Fact]
    public void HidesBothOriginalAndCurrentValuesAndPrivateProfileMetadata()
    {
        var before = DocAt(5, (0xF187, "534543524554"), (0xF189, "C0FFEE")) with
        {
            ProfileId = "PRIVATE_PROFILE_ID",
            ProfileName = "Sensitive owner"
        };
        var after = DocAt(6, (0xF187, "544F4B454E"), (0xF189, "C0FFEF")) with
        {
            ProfileId = before.ProfileId,
            ProfileName = before.ProfileName
        };
        var report = Compare(before, after);
        foreach (var privateValue in new[]
        {
            "534543524554", "544F4B454E", "C0FFEE", "C0FFEF",
            "PRIVATE_PROFILE_ID", "Sensitive owner"
        })
            Assert.DoesNotContain(privateValue, report.Summary);
        Assert.Contains("may fingerprint a vehicle", report.Summary);
        Assert.Contains("do NOT prove the same physical ECU", report.Summary);
    }

    [Fact]
    public void RejectsUnsafeHistoricalMetadataAndReturnsImmutableNoPermissionResult()
    {
        var normal = DocAt(5, (0xF187, "AA"));
        var later = DocAt(6, (0xF187, "AA"));
        foreach (var invalid in new[]
        {
            normal with { SchemaVersion = 2 },
            normal with { ContainsPotentiallySensitiveData = false },
            normal with { IsRestorableBackup = true },
            normal with { CapturedAtUtc = DateTimeOffset.MinValue }
        })
            Assert.Throws<InvalidDataException>(() => Compare(invalid, later));
        var tooMany = Enumerable.Range(0, EcuConfigurationReadback.MaximumDataIdentifiers + 1)
            .Select(i => new EcuDidReadback((ushort)(0xF100 + i), "AA")).ToArray();
        Assert.Throws<InvalidDataException>(() =>
            Compare(normal, later with { Values = tooMany }));

        var result = Compare(normal, later);
        var list = Assert.IsAssignableFrom<IList<EcuRepeatedDidObservation>>(result.Observations);
        Assert.Throws<NotSupportedException>(() =>
            list[0] = new EcuRepeatedDidObservation(0xF187, 1, 1, true));
        AssertNoEcuPermissions(result);
    }

    private static EcuConfigurationExportDocument DocAt(
        int hour, params (ushort did, string hex)[] values) =>
        new(1, false, true, "synthetic.profile", "Synthetic profile",
            "7E0", "7E8", false, false,
            new DateTimeOffset(2026, 10, 27, hour, 0, 0, TimeSpan.Zero),
            values.Select(x => new EcuDidReadback(x.did, x.hex)).ToArray());

    private static EcuRepeatedDidEvidenceReport Compare(
        EcuConfigurationExportDocument before,
        EcuConfigurationExportDocument after) =>
        EcuRepeatedDidEvidence.Compare(before, after);

    private static void AssertNoEcuPermissions(EcuRepeatedDidEvidenceReport report)
    {
        Assert.False(report.IsVerifiedCemIdentity);
        Assert.False(report.HasVerifiedFirmwareMapping);
        Assert.False(report.CanAcquireOrAuthorizePin);
        Assert.False(report.CanReadOrWriteConfiguration);
        Assert.False(report.CanSendDiagnosticRequest);
    }
}
