namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline-only snapshot comparison gates. No physical ECU, CAN
/// writes, authenticated services, PIN data, filesystem or network usage.
/// </summary>
public sealed class EcuConfigurationSnapshotDiffTests
{
    [Fact]
    public void IdenticalSnapshotsProduceNoChanges()
    {
        var original = Snapshot((0xF187, "00AB"), (0xF189, "FF"));
        var current = Snapshot((0xF187, "00AB"), (0xF189, "FF"));
        var comparison = EcuConfigurationSnapshotDiff.Compare(original, current);
        Assert.Equal(0, comparison.ChangedCount);
        Assert.All(comparison.Values, value => Assert.False(value.HasChanged));
        Assert.Equal("example.ecm", comparison.ProfileId);
    }

    [Fact]
    public void ChangedRawBytesAreRetainedInMemoryForPreview()
    {
        var comparison = EcuConfigurationSnapshotDiff.Compare(
            Snapshot((0xF187, "00AB"), (0xF189, "01")),
            Snapshot((0xF187, "00AC"), (0xF189, "01")));

        Assert.Equal(1, comparison.ChangedCount);
        var change = Assert.Single(comparison.Values.Where(x => x.HasChanged));
        Assert.Equal((ushort)0xF187, change.DataIdentifier);
        Assert.Equal("00AB", change.OriginalValueHex);
        Assert.Equal("00AC", change.CurrentValueHex);
    }

    [Fact]
    public void DidOrderAndHexLetterCaseDoNotCreateFalseChanges()
    {
        var comparison = EcuConfigurationSnapshotDiff.Compare(
            Snapshot((0xF190, "59"), (0xF187, "aa00")),
            Snapshot((0xF187, "AA00"), (0xF190, "59")));

        Assert.Equal(new ushort[] { 0xF187, 0xF190 },
            comparison.Values.Select(value => value.DataIdentifier));
        Assert.Equal(0, comparison.ChangedCount);
        Assert.Equal("AA00", comparison.Values[0].OriginalValueHex);
    }

    [Fact]
    public void DifferentProfileIdIsRejected()
    {
        var before = Snapshot((0xF187, "A1"));
        var after = before with { ProfileId = "another.module" };
        Assert.Throws<InvalidDataException>(() => EcuConfigurationSnapshotDiff.Compare(before, after));
    }

    [Fact]
    public void CanAddressAndExtendedFrameMismatchesAreRejected()
    {
        var before = Snapshot((0xF187, "A1"));
        var wrongRequest = before with { RequestId = 0x7E1 };
        var wrongResponse = before with { ResponseId = 0x7E9 };
        var wrongAddressing = before with { IsExtended = true };

        foreach (var after in new[] { wrongRequest, wrongResponse, wrongAddressing })
            Assert.Throws<InvalidDataException>(() => EcuConfigurationSnapshotDiff.Compare(before, after));
    }

    [Fact]
    public void MissingOrAdditionalDidMakesComparisonIncomplete()
    {
        var before = Snapshot((0xF187, "A1"), (0xF189, "B2"));
        var missing = Snapshot((0xF187, "A1"));
        var added = Snapshot((0xF187, "A1"), (0xF189, "B2"), (0xF190, "C3"));

        Assert.Throws<InvalidDataException>(() => EcuConfigurationSnapshotDiff.Compare(before, missing));
        Assert.Throws<InvalidDataException>(() => EcuConfigurationSnapshotDiff.Compare(before, added));
    }

    [Fact]
    public void EmptyDuplicateAndMalformedValuesAreRejected()
    {
        var valid = Snapshot((0xF187, "A1"));
        var invalid = new[]
        {
            Snapshot(),
            Snapshot((0xF187, "A1"), (0xF187, "B2")),
            Snapshot((0xF187, "")),
            Snapshot((0xF187, "A")),
            Snapshot((0xF187, "GG")),
            Snapshot((0xF187, new string('A', 16_386)))
        };

        foreach (var candidate in invalid)
        {
            Assert.Throws<InvalidDataException>(() => EcuConfigurationSnapshotDiff.Compare(candidate, valid));
            Assert.Throws<InvalidDataException>(() => EcuConfigurationSnapshotDiff.Compare(valid, candidate));
        }
    }

    [Fact]
    public void ComparisonResultIsReadOnlyAndKeepsCaptureTimes()
    {
        var before = Snapshot((0xF187, "01"));
        var after = before with
        {
            CapturedAtUtc = before.CapturedAtUtc.AddMinutes(1),
            Values = [new EcuDidReadback(0xF187, "02")]
        };

        var comparison = EcuConfigurationSnapshotDiff.Compare(before, after);
        Assert.Equal(before.CapturedAtUtc, comparison.OriginalCapturedAtUtc);
        Assert.Equal(after.CapturedAtUtc, comparison.CurrentCapturedAtUtc);
        Assert.Equal(1, comparison.ChangedCount);
        var list = Assert.IsAssignableFrom<IList<EcuDidValueDifference>>(comparison.Values);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() =>
            list.Add(new EcuDidValueDifference(0xF188, "01", "01", false)));
    }

    private static EcuConfigurationReadSnapshot Snapshot(
        params (ushort did, string hex)[] dids) =>
        new("example.ecm", "Synthetic ECM", 0x7E0, 0x7E8, false, false,
            new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
            dids.Select(item => new EcuDidReadback(item.did, item.hex)).ToArray());
}
