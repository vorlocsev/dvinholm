using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline acceptance checks for explicitly saved, deliberately lossy
/// diagnostic inventories. No J2534, ECU, PIN, network or installer required.
/// </summary>
public sealed class EcuSanitizedDidReportTests
{
    [Fact]
    public void InspectedExportProducesOnlyDidIdentifiersAndLengths()
    {
        var input = Snapshot();
        var json = EcuConfigurationSnapshotExport.CreateJson(input, true);
        var inspected = EcuConfigurationSnapshotInspection.InspectJson(json);
        var report = EcuSanitizedDidReport.Build(inspected);

        Assert.Contains("DVINHOLM REDACTED DID INVENTORY", report);
        Assert.Contains("DID count: 2", report);
        Assert.Contains("F187 | 3", report);
        Assert.Contains("F190 | 17", report);
        Assert.True(report.IndexOf("F187 | 3", StringComparison.Ordinal) <
                    report.IndexOf("F190 | 17", StringComparison.Ordinal));
        Assert.Contains("NOT a restorable ECU configuration backup", report);
    }

    [Fact]
    public void ReportDoesNotRevealPayloadIdentityOrTimestampOrAddress()
    {
        var snapshot = Snapshot();
        var report = EcuSanitizedDidReport.Build(Document(snapshot));

        Assert.DoesNotContain("PRIVATE_PROFILE", report);
        Assert.DoesNotContain("SECRET XC90", report);
        Assert.DoesNotContain("7E0", report);
        Assert.DoesNotContain("7E8", report);
        Assert.DoesNotContain("2026-10-29", report);
        Assert.DoesNotContain("53594E54484554494356494E30303131", report);
        Assert.DoesNotContain("F190 | 5359", report);
        Assert.Contains("may still fingerprint a vehicle", report);
    }

    [Fact]
    public void RejectsRestoreFlagOrUnknownSchema()
    {
        var original = Document(Snapshot());
        Assert.Throws<InvalidDataException>(() =>
            EcuSanitizedDidReport.Build(original with { IsRestorableBackup = true }));
        Assert.Throws<InvalidDataException>(() =>
            EcuSanitizedDidReport.Build(original with { SchemaVersion = 2 }));
    }

    [Fact]
    public void RejectsDuplicateOrUnsortedDidRecords()
    {
        var original = Document(Snapshot());
        var duplicate = original with
        {
            Values = [new EcuDidReadback(0xF187, "AABB"), new EcuDidReadback(0xF187, "1234")]
        };
        Assert.Throws<InvalidDataException>(() => EcuSanitizedDidReport.Build(duplicate));
        var unsorted = original with
        {
            Values = [new EcuDidReadback(0xF190, "1234"), new EcuDidReadback(0xF187, "AABB")]
        };
        Assert.Throws<InvalidDataException>(() => EcuSanitizedDidReport.Build(unsorted));
    }

    [Fact]
    public void RejectsMalformedAndOversizeHexValues()
    {
        var original = Document(Snapshot());
        foreach (var invalid in new[] { "", "A", "GG", "abcD", new string('A', 16386) })
        {
            var modified = original with { Values = [new EcuDidReadback(0xF187, invalid)] };
            Assert.Throws<InvalidDataException>(() => EcuSanitizedDidReport.Build(modified));
        }
    }

    [Fact]
    public void RejectsEmptyValuesAndNonUtcCapture()
    {
        var original = Document(Snapshot());
        Assert.Throws<InvalidDataException>(() =>
            EcuSanitizedDidReport.Build(original with { Values = [] }));
        Assert.Throws<InvalidDataException>(() =>
            EcuSanitizedDidReport.Build(original with {
                CapturedAtUtc = new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.FromHours(1))
            }));
    }

    [Fact]
    public async Task CannotWriteInsideApplicationDirectoryOrCreatePlaceholder()
    {
        var root = Path.Combine(Path.GetTempPath(), "dvinholm-redacted-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "app");
        Directory.CreateDirectory(app);
        try
        {
            var forbidden = Path.Combine(app, "report.txt");
            await Assert.ThrowsAsync<IOException>(() =>
                EcuSanitizedDidReport.SaveAsync(Document(Snapshot()), forbidden, app));
            Assert.False(File.Exists(forbidden));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExplicitSaveWritesOnlyRedactedDataOutsideApplicationTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "dvinholm-redacted-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "app");
        var reports = Path.Combine(root, "reports");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(reports);
        try
        {
            var destination = Path.Combine(reports, "inventory.txt");
            var data = Document(Snapshot());
            await EcuSanitizedDidReport.SaveAsync(data, destination, app);
            var first = await File.ReadAllTextAsync(destination, Encoding.UTF8);
            Assert.Equal(EcuSanitizedDidReport.Build(data), first);
            Assert.DoesNotContain("PRIVATE_PROFILE", first);
            Assert.DoesNotContain("53594E544845544943", first);

            var updated = data with { Values = [new EcuDidReadback(0xF187, "ABCD")] };
            await EcuSanitizedDidReport.SaveAsync(updated, destination, app);
            Assert.Equal(EcuSanitizedDidReport.Build(updated), await File.ReadAllTextAsync(destination));
            Assert.Single(Directory.GetFiles(reports));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static EcuConfigurationReadSnapshot Snapshot() => new(
        "PRIVATE_PROFILE", "SECRET XC90", 0x7E0, 0x7E8, false, false,
        new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero),
        [
            new EcuDidReadback(0xF187, "AABBCC"),
            new EcuDidReadback(0xF190, "53594E54484554494356494E3030313131")
        ]);

    private static EcuConfigurationExportDocument Document(EcuConfigurationReadSnapshot snapshot) =>
        EcuConfigurationSnapshotInspection.InspectJson(
            EcuConfigurationSnapshotExport.CreateJson(snapshot, true));
}
