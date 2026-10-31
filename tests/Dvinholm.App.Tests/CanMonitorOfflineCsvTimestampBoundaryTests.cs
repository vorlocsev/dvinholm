namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, offline-only checks for timestamp discontinuities in
/// user-selected CSVs. No physical timebase, CEM, PIN or bus I/O is trusted.
/// </summary>
public sealed class CanMonitorOfflineCsvTimestampBoundaryTests
{
    [Fact]
    public void DecreasingTimestampCannotCompleteAnEarlierFirstFrame()
    {
        var report = Offline([
            Row(100, "7E8", "10 09 11 11 11 11 11 11"),
            Row(90, "7E8", "21 22 33 44")
        ]);
        Assert.Contains("FF=1, plausible-completion=0, trailing-incomplete=0", report);
        Assert.Contains("orphan-CF=1", report);
        Assert.Contains("Timestamp regressions/wraps in CSV: 1", report);
        Assert.Contains("Active candidates invalidated at timestamp boundaries: 1", report);
        Assert.DoesNotContain("CAN 11-bit 7E8:", report);
    }

    [Fact]
    public void IncreasingTimestampsRetainOrdinaryCandidateCompletion()
    {
        var report = Offline([
            Row(100, "7E8", "10 09 11 11 11 11 11 11"),
            Row(101, "7E8", "21 22 33 44")
        ]);
        Assert.Contains("FF=1, plausible-completion=1, trailing-incomplete=0", report);
        Assert.Contains("Timestamp regressions/wraps in CSV: 0", report);
        Assert.Contains("Active candidates invalidated at timestamp boundaries: 0", report);
        Assert.Contains("Even monotonic timestamps do not validate", report);
    }

    [Fact]
    public void EqualTimestampValuesDoNotManufactureABoundary()
    {
        var report = Offline([
            Row(42, "7E8", "10 09 11 11 11 11 11 11"),
            Row(42, "7E8", "21 22 33 44")
        ]);
        Assert.Contains("plausible-completion=1", report);
        Assert.Contains("Timestamp regressions/wraps in CSV: 0", report);
    }

    [Fact]
    public void GlobalTimestampRegressionInvalidatesAllActiveCanIdGroups()
    {
        var report = Offline([
            Row(100, "7E8", "10 09 11 11 11 11 11 11"),
            Row(110, "123", "10 09 11 11 11 11 11 11"),
            Row(80, "555", "03 62 F1 90"),
            Row(81, "7E8", "21 22 33 44"),
            Row(82, "123", "21 22 33 44")
        ]);
        Assert.Contains("FF=2, plausible-completion=0, trailing-incomplete=0", report);
        Assert.Contains("orphan-CF=2", report);
        Assert.Contains("Timestamp regressions/wraps in CSV: 1", report);
        Assert.Contains("Active candidates invalidated at timestamp boundaries: 2", report);
        Assert.DoesNotContain("CAN 11-bit 7E8:", report);
        Assert.DoesNotContain("CAN 11-bit 123:", report);
        Assert.DoesNotContain("CAN 11-bit 555:", report);
    }

    [Fact]
    public void RegressionInOtherCanIdCannotBridgeFirstAndConsecutiveFrame()
    {
        var report = Offline([
            Row(200, "7E8", "10 09 11 11 11 11 11 11"),
            Row(100, "120", "00 00"),
            Row(101, "7E8", "21 22 33 44")
        ]);
        Assert.Contains("FF=1, plausible-completion=0", report);
        Assert.Contains("orphan-CF=1", report);
        Assert.Contains("Active candidates invalidated at timestamp boundaries: 1", report);
    }

    [Fact]
    public void Uint32TimestampWrapIsConservativelyTreatedAsBoundary()
    {
        var report = Offline([
            Row(uint.MaxValue - 1, "7E8", "10 09 11 11 11 11 11 11"),
            Row(0, "7E8", "21 22 33 44")
        ]);
        Assert.Contains("plausible-completion=0", report);
        Assert.Contains("Timestamp regressions/wraps in CSV: 1", report);
        Assert.Contains("uint32 wrap", report);
        Assert.DoesNotContain(uint.MaxValue.ToString(), report);
    }

    [Fact]
    public void RegressionsWithoutActiveCandidatesStillReportUnverifiedBoundaries()
    {
        var report = Offline([
            Row(100, "7E8", "03 62 F1 90"),
            Row(90, "7E8", "03 62 F1 90"),
            Row(80, "120", "CF 40 E3 02")
        ]);
        Assert.Contains("Timestamp regressions/wraps in CSV: 2", report);
        Assert.Contains("Active candidates invalidated at timestamp boundaries: 0", report);
        Assert.Contains("FF=0, plausible-completion=0", report);
        Assert.Contains("not verified ISO-TP sessions", report);
    }

    [Fact]
    public void CsvRoundTripPreservesConservativeBoundaryAndLiveApiBehavior()
    {
        var frames = new[]
        {
            Row(250, "7E8", "10 09 59 56 31 43 4D 35"),
            Row(10, "7E8", "21 39 48 00"),
            Row(11, "120", "CF 50 E3 02")
        };
        var csv = string.Join("\n", CanMonitorCsv.BuildLines(frames, false));
        var snapshot = CanMonitorOfflineCsvInspection.InspectCsv(csv);
        var report = CanMonitorOfflineCsvInspection.BuildRedactedReport(snapshot);
        Assert.Contains("POTENTIAL ISO-TP SEQUENCES", report);
        Assert.Contains("plausible-completion=0", report);
        Assert.Contains("Timestamp regressions/wraps in CSV: 1", report);
        Assert.Contains("candidate-E3-02=1", report);
        Assert.DoesNotContain("59 56 31", report);
        Assert.DoesNotContain("39 48", report);
        Assert.DoesNotContain("7E8", report);
        Assert.DoesNotContain("CAN 11-bit 120", report);
        Assert.DoesNotContain("250", report);

        // Existing live summary has no new CSV timestamp-policy dependency.
        var live = CanMonitorIsoTpCandidateSequences.Build(
            frames, frames.Length, 0, syntheticDemo: false);
        Assert.Contains("FF=1, plausible-completion=1", live);
        Assert.Contains("CAN 11-bit 7E8:", live);
        Assert.DoesNotContain("Timestamp regressions/wraps in CSV:", live);
    }

    private static CanMonitorRow Row(uint timestamp, string id, string data) =>
        new(timestamp, id, "11-bit", (uint)((data.Length + 1) / 3), data);

    private static string Offline(IReadOnlyList<CanMonitorRow> rows) =>
        CanMonitorIsoTpCandidateSequences.BuildAggregate(
            rows, rows.Count, null, syntheticDemo: false);
}
