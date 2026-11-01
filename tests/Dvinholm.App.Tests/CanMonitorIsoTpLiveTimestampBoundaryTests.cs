namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic RX-history checks. Timestamp discontinuity is evidence
/// of uncertain capture continuity, not proof of an ISO-TP or CEM session.
/// </summary>
public sealed class CanMonitorIsoTpLiveTimestampBoundaryTests
{
    [Fact]
    public void MonotonicLiveFirstAndConsecutiveFramesStillPlausiblyComplete()
    {
        var report = Live([
            Frame(100, "7E8", "10 09 11 11 11 11 11 11"),
            Frame(101, "120", "03 62 F1 90"),
            Frame(102, "7E8", "21 22 33 44")
        ]);
        Assert.Contains("FF=1, plausible-completion=1", report);
        Assert.Contains("Timestamp regressions/wraps in RX rows: 0", report);
        Assert.Contains("Active candidates invalidated at RX timestamp boundaries: 0", report);
    }

    [Fact]
    public void BackwardsJumpOnSameCanIdCannotFinishAnEarlierFirstFrame()
    {
        var report = Live([
            Frame(200, "7E8", "10 09 11 11 11 11 11 11"),
            Frame(100, "7E8", "21 22 33 44")
        ]);
        Assert.Contains("FF=1, plausible-completion=0", report);
        Assert.Contains("trailing-incomplete=0", report);
        Assert.Contains("orphan-CF=1", report);
        Assert.Contains("Timestamp regressions/wraps in RX rows: 1", report);
        Assert.Contains("Active candidates invalidated at RX timestamp boundaries: 1", report);
    }

    [Fact]
    public void BackwardsJumpOnOtherCanIdInvalidatesPriorCandidateGlobally()
    {
        var report = Live([
            Frame(200, "7E8", "10 09 11 11 11 11 11 11"),
            Frame(100, "120", "03 62 F1 90"),
            Frame(101, "7E8", "21 22 33 44")
        ]);
        Assert.Contains("CAN 11-bit 7E8: candidate FF=1, plausible-completion=0", report);
        Assert.Contains("orphan-CF=1", report);
        Assert.Contains("Active candidates invalidated at RX timestamp boundaries: 1", report);
    }

    [Fact]
    public void TimestampUint32WrapCannotLinkCandidatesAcrossClockBoundary()
    {
        var report = Live([
            Frame(uint.MaxValue - 1, "7E8", "10 09 11 11 11 11 11 11"),
            Frame(0, "7E8", "21 22 33 44")
        ]);
        Assert.Contains("plausible-completion=0", report);
        Assert.Contains("Timestamp regressions/wraps in RX rows: 1", report);
        Assert.DoesNotContain((uint.MaxValue - 1).ToString(), report);
    }

    [Fact]
    public void EqualTimeStampsDoNotInventAClockReset()
    {
        var report = Live([
            Frame(42, "7E8", "10 09 11 11 11 11 11 11"),
            Frame(42, "7E8", "21 22 33 44")
        ]);
        Assert.Contains("plausible-completion=1", report);
        Assert.Contains("Timestamp regressions/wraps in RX rows: 0", report);
        Assert.Contains("NOT a validated diagnostic exchange", report);
    }

    [Fact]
    public void SingleGlobalBoundaryClearsTwoActiveGroupsButAllowsFreshSequence()
    {
        var report = Live([
            Frame(300, "7E8", "10 09 11 11 11 11 11 11"),
            Frame(310, "123", "10 09 11 11 11 11 11 11"),
            Frame(100, "456", "03 62 F1 90"),
            Frame(101, "7E8", "21 22 33 44"),
            Frame(102, "123", "21 22 33 44"),
            Frame(103, "7E8", "10 09 11 11 11 11 11 11"),
            Frame(104, "7E8", "21 22 33 44")
        ]);
        Assert.Contains("FF=3, plausible-completion=1", report);
        Assert.Contains("orphan-CF=2", report);
        Assert.Contains("Active candidates invalidated at RX timestamp boundaries: 2", report);
        Assert.Contains("Timestamp regressions/wraps in RX rows: 1", report);
    }

    [Fact]
    public void MultipleBackwardsJumpsWithoutActiveFramesReportZeroInvalidations()
    {
        var report = Live([
            Frame(99, "7E8", "03 62 F1 90"),
            Frame(80, "7E8", "03 62 F1 90"),
            Frame(60, "123", "03 62 F1 90")
        ]);
        Assert.Contains("Timestamp regressions/wraps in RX rows: 2", report);
        Assert.Contains("Active candidates invalidated at RX timestamp boundaries: 0", report);
        Assert.Contains("FF=0, plausible-completion=0", report);
    }

    [Fact]
    public void CsvAndLiveReportsShareSafetyButOnlyCsvWithholdsCanId()
    {
        var rows = new[]
        {
            Frame(876543210, "7E8", "10 09 59 56 31 43 4D 35"),
            Frame(123456789, "7E8", "21 39 48 00")
        };
        var live = Live(rows);
        var csv = CanMonitorIsoTpCandidateSequences.BuildAggregate(
            rows, rows.Length, 0, false);
        Assert.Contains("CAN 11-bit 7E8:", live);
        Assert.Contains("Timestamp regressions/wraps in RX rows: 1", live);
        Assert.Contains("Timestamp regressions/wraps in CSV: 1", csv);
        Assert.Contains("plausible-completion=0", live);
        Assert.Contains("plausible-completion=0", csv);
        Assert.DoesNotContain("CAN 11-bit 7E8:", csv);
        Assert.DoesNotContain("59 56 31", live);
        Assert.DoesNotContain("39 48", csv);
        Assert.DoesNotContain("876543210", live);
        Assert.DoesNotContain("123456789", csv);
        Assert.Contains("No CAN/J2534 operation", live);
    }

    private static CanMonitorRow Frame(uint ms, string id, string data) =>
        new(ms, id, "11-bit", (uint)((data.Length + 1) / 3), data);

    private static string Live(IReadOnlyList<CanMonitorRow> rows) =>
        CanMonitorIsoTpCandidateSequences.Build(
            rows, rows.Count, 0, syntheticDemo: false);
}
