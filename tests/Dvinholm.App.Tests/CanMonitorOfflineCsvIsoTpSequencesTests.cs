namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic checks for the ID-redacted offline CSV extension.
/// This cannot authenticate ISO-TP, Volvo D2, ECU identity or capture order.
/// </summary>
public sealed class CanMonitorOfflineCsvIsoTpSequencesTests
{
    [Fact]
    public void EmptyInputHasNoClaimsOfValidMultiFrameTraffic()
    {
        var report = Aggregate([]);
        Assert.Contains("Buffered RX rows: 0", report);
        Assert.Contains("All-group candidate totals (including hidden groups): FF=0", report);
        Assert.Contains("Offline CSV aggregate mode: CAN identifiers withheld.", report);
        Assert.Contains("NOT a validated diagnostic exchange", report);
        Assert.DoesNotContain("CAN 11-bit 7E8:", report);
    }

    [Fact]
    public void SameIdFirstAndConsecutiveFramesMayCompleteWithoutExposingId()
    {
        var report = Aggregate([
            Row("7E8", "10 09 59 56 31 43 4D 35"),
            Row("123", "03 62 F1 90"), // Interleaved other CAN ID.
            Row("7E8", "21 39 48 00")
        ]);
        Assert.Contains("FF=1, plausible-completion=1, trailing-incomplete=0", report);
        Assert.Contains("sequence-mismatch=0, interrupted=0, orphan-CF=0", report);
        Assert.DoesNotContain("CAN 11-bit 7E8", report);
        Assert.DoesNotContain("CAN 11-bit 123", report);
        Assert.DoesNotContain("59 56 31", report);
        Assert.DoesNotContain("62 F1 90", report);
        Assert.DoesNotContain("39 48", report);
        Assert.DoesNotContain("7E8", report);
        Assert.DoesNotContain("123", report);
    }

    [Fact]
    public void CrossIdConsecutiveFrameCannotCompleteFirstFrame()
    {
        var report = Aggregate([
            Row("7E8", "10 09 11 11 11 11 11 11"),
            Row("7E9", "21 22 33 44")
        ]);
        Assert.Contains("FF=1, plausible-completion=0, trailing-incomplete=1", report);
        Assert.Contains("orphan-CF=1", report);
        Assert.DoesNotContain("7E8", report);
        Assert.DoesNotContain("7E9", report);
    }

    [Fact]
    public void SameNumericIdAcrossElevenAndTwentyNineBitRemainIndependent()
    {
        var report = Aggregate([
            Row("7E8", "10 09 11 11 11 11 11 11"),
            Row("000007E8", "21 22 33 44", extended: true)
        ]);
        Assert.Contains("Distinct groups: 2", report);
        Assert.Contains("FF=1, plausible-completion=0, trailing-incomplete=1", report);
        Assert.Contains("orphan-CF=1", report);
        Assert.DoesNotContain("000007E8", report);
        Assert.DoesNotContain("7E8", report);
    }

    [Fact]
    public void SequenceMismatchAndInterruptionRetainConservativeTotals()
    {
        var report = Aggregate([
            Row("111", "10 09 11 11 11 11 11 11"),
            Row("111", "22 22 33 44"), // Wrong SN.
            Row("222", "10 09 11 11 11 11 11 11"),
            Row("222", "03 62 F1 90") // A different shape interrupts.
        ]);
        Assert.Contains("FF=2, plausible-completion=0, trailing-incomplete=0", report);
        Assert.Contains("sequence-mismatch=1, interrupted=1, orphan-CF=0", report);
        Assert.DoesNotContain("CAN 11-bit 111", report);
        Assert.DoesNotContain("CAN 11-bit 222", report);
    }

    [Fact]
    public void MoreThan64IdGroupsStillContributeToTotalsWithoutIdList()
    {
        var rows = Enumerable.Range(0, 65)
            .SelectMany(i =>
                new[] {
                    Row(i.ToString("X3"), "10 09 11 11 11 11 11 11"),
                    Row(i.ToString("X3"), "21 22 33 44")
                }).ToArray();
        var report = Aggregate(rows);
        Assert.Contains("Distinct groups: 65", report);
        Assert.Contains("FF=65, plausible-completion=65, trailing-incomplete=0", report);
        Assert.DoesNotContain("CAN 11-bit 03F:", report);
        Assert.DoesNotContain("CAN 11-bit 040:", report);
        Assert.DoesNotContain("Additional groups withheld:", report);
        Assert.True(report.Length < 2500);
    }

    [Fact]
    public void UnknownCaptureLossAndSyntheticDemoCannotBeReportedAsVerified()
    {
        var rows = new[] { Row("7E8", "10 09 11 11 11 11 11 11") };
        var report = CanMonitorIsoTpCandidateSequences.BuildAggregate(
            rows, 4, null, syntheticDemo: true);
        Assert.Contains("SYNTHETIC DEMO", report);
        Assert.Contains("History rows evicted: 3", report);
        Assert.Contains("Subscriber RX drops: UNKNOWN", report);
        Assert.Contains("Observed window quality: INCOMPLETE", report);
        Assert.Contains("Frame continuity UNKNOWN/INCOMPLETE", report);
        var unknown = Aggregate(rows);
        Assert.Contains("Observed window quality: UNKNOWN", unknown);
        Assert.DoesNotContain("NO REPORTED LOSS", unknown);
        Assert.Throws<InvalidDataException>(() =>
            CanMonitorIsoTpCandidateSequences.BuildAggregate(rows, 0, null, false));
    }

    [Fact]
    public void ExportedOfflineCsvReportIncludesAggregatesAndLiveReportStillShowsIds()
    {
        var captured = new[]
        {
            Row("7E8", "10 09 11 11 11 11 11 11"),
            Row("7E8", "21 59 56 31"),
            Row("120", "CF 50 E3 02")
        };
        var csv = string.Join("\n", CanMonitorCsv.BuildLines(captured, false));
        var imported = CanMonitorOfflineCsvInspection.InspectCsv(csv);
        var report = CanMonitorOfflineCsvInspection.BuildRedactedReport(imported);
        Assert.Contains("POTENTIAL ISO-TP FRAME SHAPES", report);
        Assert.Contains("POTENTIAL ISO-TP SEQUENCES", report);
        Assert.Contains("All-group candidate totals", report);
        Assert.Contains("FF=1, plausible-completion=1", report);
        Assert.Contains("candidate-E3-02=1", report);
        Assert.DoesNotContain("7E8", report);
        Assert.DoesNotContain("120", report);
        Assert.DoesNotContain("59 56 31", report);
        Assert.False(imported.AuthenticatesVehicleOrCem);
        Assert.False(imported.AllowsAdapterConnection);
        var live = CanMonitorIsoTpCandidateSequences.Build(
            captured, captured.Length, 0, false);
        Assert.Contains("CAN 11-bit 7E8:", live);
        Assert.Contains("No source ECU", live);

        var source = ReadSource("src/Dvinholm.App/CanMonitorOfflineCsvInspection.cs");
        Assert.Contains("CanMonitorIsoTpCandidateSequences.BuildAggregate(", source);
        var ui = ReadSource("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("CanMonitorOfflineCsvInspection.BuildRedactedReport(", ui);
    }

    private static string Aggregate(IReadOnlyList<CanMonitorRow> rows) =>
        CanMonitorIsoTpCandidateSequences.BuildAggregate(
            rows, rows.Count, null, syntheticDemo: false);

    private static CanMonitorRow Row(
        string id, string data, bool extended = false) =>
        new(42, id, extended ? "29-bit" : "11-bit",
            (uint)((data.Length + 1) / 3), data);

    private static string ReadSource(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var full = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
                return File.ReadAllText(full);
        }
        throw new FileNotFoundException(relative);
    }
}
