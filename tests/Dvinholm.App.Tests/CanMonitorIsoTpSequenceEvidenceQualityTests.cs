namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for explicit evidence-quality flags in the existing
/// heuristic ISO-TP RX sequence summary. No CEM or vehicle is identified.
/// </summary>
public sealed class CanMonitorIsoTpSequenceEvidenceQualityTests
{
    [Fact]
    public void EmptyWindowWithKnownZeroDropsStillDoesNotClaimPhysicalCompleteness()
    {
        var text = Report([], 0, 0);
        Assert.Contains("History rows evicted: 0", text);
        Assert.Contains("Subscriber RX drops: 0", text);
        Assert.Contains("NO REPORTED LOSS within the retained monitor window", text);
        Assert.Contains("NOT a full bus capture", text);
        Assert.Contains("All-group candidate totals (including hidden groups): FF=0", text);
        Assert.Contains("cannot prove that all physical bus frames were received", text);
    }

    [Fact]
    public void HistoryEvictionOverridesApparentlyCompleteSequence()
    {
        var rows = CompletedCandidate();
        var text = Report(rows, 20, 0);
        Assert.Contains("History rows evicted: 18", text);
        Assert.Contains("Subscriber RX drops: 0", text);
        Assert.Contains("Observed window quality: INCOMPLETE", text);
        Assert.Contains("plausible-completion=1", text);
        Assert.Contains("Frame continuity UNKNOWN/INCOMPLETE", text);
        Assert.Contains("NOT a validated diagnostic exchange", text);
    }

    [Fact]
    public void NonzeroSubscriberDropsMarkWindowIncompleteWithoutHistoryEviction()
    {
        var rows = CompletedCandidate();
        var text = Report(rows, rows.Length, 3);
        Assert.Contains("History rows evicted: 0", text);
        Assert.Contains("Subscriber RX drops: 3", text);
        Assert.Contains("Observed window quality: INCOMPLETE", text);
        Assert.Contains("All-group candidate totals (including hidden groups): FF=1", text);
    }

    [Fact]
    public void UnknownDropCounterNeverBecomesZeroOrClean()
    {
        var rows = CompletedCandidate();
        var text = Report(rows, rows.Length, null);
        Assert.Contains("Subscriber RX drops: UNKNOWN", text);
        Assert.Contains("Observed window quality: UNKNOWN: subscriber drop counter is unavailable", text);
        Assert.DoesNotContain("Observed window quality: NO REPORTED LOSS", text);
        Assert.Contains("Frame continuity UNKNOWN/INCOMPLETE", text);
    }

    [Fact]
    public void EvictionAndUnknownDropCounterReportKnownIncompleteNotMerelyUnknown()
    {
        var rows = CompletedCandidate();
        var text = Report(rows, rows.Length + 1, null);
        Assert.Contains("History rows evicted: 1", text);
        Assert.Contains("Subscriber RX drops: UNKNOWN", text);
        Assert.Contains("Observed window quality: INCOMPLETE", text);
        Assert.DoesNotContain("Observed window quality: UNKNOWN:", text);
    }

    [Fact]
    public void HiddenGroupsStillCountTowardsAllGroupTotals()
    {
        var rows = Enumerable.Range(0, 65)
            .Select(i => Frame(i.ToString("X3"), "10 09 11 11 11 11 11 11"))
            .ToArray();
        var text = Report(rows, rows.Length, 0);
        Assert.Contains("Distinct groups: 65", text);
        Assert.Contains("Additional groups withheld: 1", text);
        Assert.Contains("All-group candidate totals (including hidden groups): FF=65, plausible-completion=0, trailing-incomplete=65", text);
        Assert.Contains("CAN 11-bit 03F", text);
        Assert.DoesNotContain("CAN 11-bit 040:", text);
        Assert.DoesNotContain("11 11 11", text);
    }

    [Fact]
    public void AllGroupCountersDistinguishCompletionMismatchInterruptionAndOrphans()
    {
        var rows = new[]
        {
            Frame("7E8", "10 09 11 11 11 11 11 11"),
            Frame("7E8", "21 22 33 44"),
            Frame("123", "21 11 11"),
            Frame("321", "10 20 11 11 11 11 11 11"),
            Frame("321", "22 11 11"),
            Frame("456", "10 20 11 11 11 11 11 11"),
            Frame("456", "03 62 F1 90")
        };
        var text = Report(rows, rows.Length, 0);
        Assert.Contains("All-group candidate totals (including hidden groups): FF=3, plausible-completion=1, trailing-incomplete=0, sequence-mismatch=1, interrupted=1, orphan-CF=1", text);
        Assert.DoesNotContain("22 33 44", text);
        Assert.DoesNotContain("03 62 F1 90", text);
    }

    [Fact]
    public void LargeHistoricalReceiveCounterNeverOverflowsEvictionAccounting()
    {
        var rows = new[] { Frame("123", "21 11") };
        var text = Report(rows, long.MaxValue, 0);
        Assert.Contains("History rows evicted: 9223372036854775806", text);
        Assert.Contains("Observed window quality: INCOMPLETE", text);
        Assert.Contains("All-group candidate totals (including hidden groups): FF=0", text);
        Assert.DoesNotContain("NO REPORTED LOSS within", text);
        Assert.True(text.Length < 12000);
    }

    private static CanMonitorRow[] CompletedCandidate() =>
    [
        Frame("7E8", "10 09 11 11 11 11 11 11"),
        Frame("7E8", "21 22 33 44")
    ];

    private static CanMonitorRow Frame(string id, string bytes) =>
        new(100, id, id.Length == 8 ? "29-bit" : "11-bit",
            (uint)((bytes.Length + 1) / 3), bytes);

    private static string Report(
        IReadOnlyList<CanMonitorRow> rows, long received, long? drops) =>
        CanMonitorIsoTpCandidateSequences.Build(
            rows, received, drops, syntheticDemo: false);
}
