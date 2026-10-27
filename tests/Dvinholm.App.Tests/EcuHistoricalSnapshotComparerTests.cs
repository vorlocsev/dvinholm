namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, offline comparisons with raw DID values never exposed.
/// No vehicle, adapter, PIN, filesystem write or ECU authorization exists here.
/// </summary>
public sealed class EcuHistoricalSnapshotComparerTests
{
    [Fact]
    public void EqualHistoricalSnapshotsReportNoChanges()
    {
        var result = EcuHistoricalSnapshotComparer.Compare(
            Doc((0xF187, "0102"), (0xF189, "AABB")),
            Doc((0xF187, "0102"), (0xF189, "AABB")));
        Assert.Equal(2, result.ComparedCount);
        Assert.Equal(0, result.ChangedCount);
        Assert.Empty(result.ChangedDataIdentifiers);
        Assert.Contains("none", result.Summary);
    }

    [Fact]
    public void MultipleChangedDidsAreListedInAscendingOrder()
    {
        var result = EcuHistoricalSnapshotComparer.Compare(
            Doc((0xF189, "BB"), (0xF187, "AA"), (0xF190, "31")),
            Doc((0xF190, "32"), (0xF187, "AB"), (0xF189, "BB")));
        Assert.Equal(3, result.ComparedCount);
        Assert.Equal(2, result.ChangedCount);
        Assert.Equal(new ushort[] { 0xF187, 0xF190 }, result.ChangedDataIdentifiers);
        Assert.Contains("F187, F190", result.Summary);
    }

    [Fact]
    public void ReportHidesBothOriginalAndCurrentRawPayloads()
    {
        var result = EcuHistoricalSnapshotComparer.Compare(
            Doc((0xF187, "534543524554"), (0xF189, "C0FFEE")),
            Doc((0xF187, "544F4B454E"), (0xF189, "C0FFEF")));
        foreach (var privateHex in new[] { "534543524554", "544F4B454E", "C0FFEE", "C0FFEF" })
            Assert.DoesNotContain(privateHex, result.Summary);
        Assert.Contains("values hidden", result.Summary);
    }

    [Fact]
    public void ReportDoesNotExposeProfileNameOrId()
    {
        var before = Doc((0xF187, "AA")) with
        {
            ProfileId = "SENSITIVE_PROFILE_ID",
            ProfileName = "SENSITIVE_OWNER_NAME"
        };
        var after = before with { Values = [new EcuDidReadback(0xF187, "AB")] };
        var report = EcuHistoricalSnapshotComparer.Compare(before, after).Summary;
        Assert.DoesNotContain("SENSITIVE_PROFILE_ID", report);
        Assert.DoesNotContain("SENSITIVE_OWNER_NAME", report);
    }

    [Fact]
    public void DifferentProfilesOrAddressingCannotBeCompared()
    {
        var before = Doc((0xF187, "AA"));
        var variants = new[]
        {
            before with { ProfileId = "different" },
            before with { RequestCanId = "7E1" },
            before with { ResponseCanId = "7E9" },
            before with { IsExtended = true, RequestCanId = "000007E0", ResponseCanId = "000007E8" }
        };
        foreach (var after in variants)
            Assert.Throws<InvalidDataException>(() =>
                EcuHistoricalSnapshotComparer.Compare(before, after));
    }

    [Fact]
    public void IncompleteOrDuplicateDidSetMustBeRejected()
    {
        var before = Doc((0xF187, "AA"), (0xF189, "BB"));
        var alternatives = new[]
        {
            Doc((0xF187, "AA")),
            Doc((0xF187, "AA"), (0xF189, "BB"), (0xF190, "CC")),
            Doc((0xF187, "AA"), (0xF187, "BB")),
            Doc((0xF187, "AA"), (0xF189, "ZZ"))
        };
        foreach (var after in alternatives)
            Assert.Throws<InvalidDataException>(() =>
                EcuHistoricalSnapshotComparer.Compare(before, after));
    }

    [Fact]
    public void RejectsInvalidUtcVersionAndBackupOrSensitivityFlags()
    {
        var good = Doc((0xF187, "AA"));
        var invalid = new[]
        {
            good with { SchemaVersion = 2 },
            good with { IsRestorableBackup = true },
            good with { ContainsPotentiallySensitiveData = false },
            good with { CapturedAtUtc = DateTimeOffset.MinValue },
            good with { CapturedAtUtc = good.CapturedAtUtc.ToOffset(TimeSpan.FromHours(3)) },
            good with { RequestCanId = "800" }
        };
        foreach (var candidate in invalid)
            Assert.Throws<InvalidDataException>(() =>
                EcuHistoricalSnapshotComparer.Compare(good, candidate));
    }

    [Fact]
    public void ResultIsImmutableAndMakesNoHardwareOrRestoreClaims()
    {
        var before = Doc((0xF187, "AA"));
        var after = Doc((0xF187, "AB"));
        var result = EcuHistoricalSnapshotComparer.Compare(before, after);
        Assert.Contains("does NOT prove the same physical ECU", result.Summary);
        Assert.Contains("No configuration can be restored", result.Summary);
        Assert.Contains("SHA-256 does not authenticate", result.Summary);
        var ids = Assert.IsAssignableFrom<IList<ushort>>(result.ChangedDataIdentifiers);
        Assert.True(ids.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ids.Add(0xF190));
    }

    private static EcuConfigurationExportDocument Doc(
        params (ushort did, string hex)[] values) =>
        new(1, false, true, "synthetic.ecm", "PRIVATE_SYNTHETIC_NAME",
            "7E0", "7E8", false, false,
            new DateTimeOffset(2026, 10, 27, 5, 0, 0, TimeSpan.Zero),
            values.Select(x => new EcuDidReadback(x.did, x.hex)).ToArray());
}
