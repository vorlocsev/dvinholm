namespace Dvinholm.App.Tests;

/// <summary>
/// Eight privacy/UI-format tests on synthetic historical files only.
/// These contain no real VIN, J2534 or ECU operations.
/// </summary>
public sealed class EcuSnapshotInspectionDisplayTests
{
    [Fact]
    public void ShowsOnlyHistoricalCaptureDateAndDidIdentifiers()
    {
        var summary = EcuSnapshotInspectionDisplay.Build(Document());
        Assert.Contains("2026-10-27 05:00:00", summary);
        Assert.Contains("DID count: 2", summary);
        Assert.Contains("F187, F189", summary);
    }

    [Fact]
    public void DoesNotRevealRawDidValuesInDialog()
    {
        var summary = EcuSnapshotInspectionDisplay.Build(Document());
        Assert.DoesNotContain("534543524554", summary);
        Assert.DoesNotContain("004142", summary);
        Assert.Contains("values hidden", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DoesNotEchoPotentiallySensitiveProfileNamesOrIds()
    {
        var summary = EcuSnapshotInspectionDisplay.Build(
            Document() with { ProfileName = "Sensitive vehicle owner", ProfileId = "MY_PRIVATE_VIN" });

        Assert.DoesNotContain("Sensitive vehicle owner", summary);
        Assert.DoesNotContain("MY_PRIVATE_VIN", summary);
    }

    [Fact]
    public void DisplaysCanIdsAndExplicitFrameFormat()
    {
        var standard = EcuSnapshotInspectionDisplay.Build(Document());
        Assert.Contains("7E0 / 7E8 (11-bit)", standard);

        var extended = EcuSnapshotInspectionDisplay.Build(Document() with
        {
            RequestCanId = "18DA10F1",
            ResponseCanId = "18DAF110",
            IsExtended = true
        });
        Assert.Contains("18DA10F1 / 18DAF110 (29-bit)", extended);
    }

    [Fact]
    public void HistoricalFileNeverClaimsTrustedSourceOrRestore()
    {
        var text = EcuSnapshotInspectionDisplay.Build(Document());
        Assert.Contains("does NOT prove the file source", text);
        Assert.Contains("Not a restorable", text);
        Assert.Contains("Historical diagnostic data only", text);
    }

    [Fact]
    public void UnsupportedVersionAndRestorableClaimAreRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            EcuSnapshotInspectionDisplay.Build(Document() with { SchemaVersion = 2 }));
        Assert.Throws<InvalidDataException>(() =>
            EcuSnapshotInspectionDisplay.Build(Document() with { IsRestorableBackup = true }));
    }

    [Fact]
    public void MissingSensitivityMarkAndNonUtcTimeAreRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            EcuSnapshotInspectionDisplay.Build(Document() with
            {
                ContainsPotentiallySensitiveData = false
            }));
        Assert.Throws<InvalidDataException>(() =>
            EcuSnapshotInspectionDisplay.Build(Document() with
            {
                CapturedAtUtc = new DateTimeOffset(2026, 10, 27, 5, 0, 0,
                    TimeSpan.FromHours(2))
            }));
    }

    [Fact]
    public void NullEmptyAndExcessiveRecordsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            EcuSnapshotInspectionDisplay.Build(null!));
        Assert.Throws<InvalidDataException>(() =>
            EcuSnapshotInspectionDisplay.Build(Document() with { Values = [] }));

        var tooMany = Enumerable.Range(0, 129)
            .Select(i => new EcuDidReadback((ushort)(0xF100 + i), "AA"))
            .ToArray();
        Assert.Throws<InvalidDataException>(() =>
            EcuSnapshotInspectionDisplay.Build(Document() with { Values = tooMany }));
    }

    private static EcuConfigurationExportDocument Document() =>
        new(1, false, true, "synthetic.ecm", "Synthetic ECM",
            "7E0", "7E8", false, false,
            new DateTimeOffset(2026, 10, 27, 5, 0, 0, TimeSpan.Zero),
            [
                new EcuDidReadback(0xF187, "004142"),
                new EcuDidReadback(0xF189, "534543524554")
            ]);
}
