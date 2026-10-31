using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, offline-only tests for a conservative temporal
/// association limit. They are NOT proof of ECU response or CEM identity.
/// </summary>
public sealed class J2534OfflineTraceTemporalWindowTests
{
    [Fact]
    public void RecentReadIsCountedOnlyAsTemporalEvidence()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 100, 71, count: 1, category: "a3_tx"),
            Event("read", 29_999, 71, count: 1));
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
        Assert.Contains("Stale accepted-TX windows expired on data read: 0", report);
        Assert.Contains("NOT verified ECU response", report);
    }

    [Fact]
    public void ExactThirtySecondBoundaryRemainsEligible()
    {
        var report = Inspect(
            Event("connect", 10, 71),
            Event("write", 100, 71, count: 1),
            Event("read", 100 + J2534OfflineTraceInspection.MaximumTemporalAssociationMs,
                71, count: 1));
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 0", report);
    }

    [Fact]
    public void AReadOneMillisecondBeyondLimitCannotClaimOldWrite()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 100, 71, count: 1),
            Event("read", 100 + J2534OfflineTraceInspection.MaximumTemporalAssociationMs + 1,
                71, count: 1));
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 1", report);
        Assert.Contains("Stale accepted-TX windows expired on data read: 1", report);
    }

    [Fact]
    public void StaleWindowIsRemovedOnlyOnceForSuccessiveReads()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 2, 71, count: 1),
            Event("read", 30_004, 71, count: 1),
            Event("read", 30_005, 71, count: 1));
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 2", report);
        Assert.Contains("Stale accepted-TX windows expired on data read: 1", report);
    }

    [Fact]
    public void FreshWriteReplacesOldWindow()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 2, 71, count: 1),
            Event("write", 30_050, 71, count: 1),
            Event("read", 30_051, 71, count: 1));
        Assert.Contains("Writes observed / accepted / unsuccessful: 2 / 2 / 0", report);
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
        Assert.Contains("Stale accepted-TX windows expired on data read: 0", report);
    }

    [Fact]
    public void OneChannelStalesWithoutRevokingRecentOtherChannel()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("connect", 2, 72),
            Event("write", 3, 71, count: 1),
            Event("write", 30_010, 72, count: 1),
            Event("read", 30_011, 71, count: 1),
            Event("read", 30_012, 72, count: 1));
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 1", report);
        Assert.Contains("Stale accepted-TX windows expired on data read: 1", report);
    }

    [Fact]
    public void TimestampWrapCannotRevivePreviousTemporalWindow()
    {
        var report = Inspect(
            Event("connect", uint.MaxValue - 2, 71),
            Event("write", uint.MaxValue - 1, 71, count: 1),
            Event("read", 0, 71, count: 1),
            Event("connect", 1, 71),
            Event("read", 2, 71, count: 1));
        Assert.Contains("Timestamp regressions/wraps: 1; invalidated TX windows: 1", report);
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("Stale accepted-TX windows expired on data read: 0", report);
    }

    [Fact]
    public void ExpiryReportDoesNotRevealPrivateIdentifiersOrRawTime()
    {
        var report = Inspect(
            Event("connect", 1, 7654321),
            Event("write", 2, 7654321, count: 1, category: "b9fb_tx"),
            Event("read", 40_123, 7654321, count: 1, category: "f9f0_candidate"));
        Assert.Contains("Stale accepted-TX windows expired on data read: 1", report);
        Assert.Contains("Temporal proximity limit: 30000 ms", report);
        Assert.Contains("heuristic only", report);
        Assert.DoesNotContain("7654321", report);
        Assert.DoesNotContain("40123", report);
        Assert.DoesNotContain("0x0BADF00D", report);
        Assert.DoesNotContain("b9fb_tx", report);
    }

    private static string Inspect(params string[] events) =>
        J2534OfflineTraceInspection.BuildRedactedReport(string.Join("\n", events));

    private static string Event(string kind, uint ms, uint channel,
        uint status = 0, uint requested = 1, uint count = 0,
        string category = "other") =>
        JsonSerializer.Serialize(new
        {
            schema = 15, ms, @event = kind, channel, status, requested, count,
            @class = category, can_id = "0x0BADF00D", size = 0, value = 0,
            core_version = 26
        });
}
