using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic schema-15 batch-count integrity tests. This tool never
/// sends J2534 calls and does not identify any actual vehicle or CEM.
/// </summary>
public sealed class J2534OfflineTraceBatchCountTests
{
    [Fact]
    public void ConsistentSingleMessageWriteAndReadRetainTemporalEvidence()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 2, 71, requested: 1, count: 1, category: "a3_tx"),
            Event("read", 3, 71, requested: 1, count: 1, category: "e3_02_candidate"));
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
        Assert.Contains("Inconsistent TX / RX batch counts (returned > requested): 0 / 0", report);
        Assert.Contains("Writes observed / accepted / unsuccessful: 1 / 1 / 0", report);
    }

    [Fact]
    public void OverReturnedWriteNeverCreatesAcceptedTransmitWindow()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 2, 71, requested: 1, count: 2, category: "b9fb_tx"),
            Event("read", 3, 71, requested: 1, count: 1));
        Assert.Contains("Inconsistent TX / RX batch counts (returned > requested): 1 / 0", report);
        Assert.Contains("Writes observed / accepted / unsuccessful: 1 / 0 / 1", report);
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 1", report);
        Assert.Contains("B9FB / B9E0 / B9F0 / B9FC: 0 / 0 / 0 / 0", report);
    }

    [Fact]
    public void NonzeroWriteCountWithZeroRequestIsUntrusted()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 2, 71, requested: 0, count: 1, category: "a3_tx"),
            Event("read", 3, 71, requested: 1, count: 1));
        Assert.Contains("Inconsistent TX / RX batch counts (returned > requested): 1 / 0", report);
        Assert.Contains("Writes observed / accepted / unsuccessful: 1 / 0 / 1", report);
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("A3 / B9FB / B9E0 / B9F0 / B9FC: 0 / 0 / 0 / 0 / 0", report);
    }

    [Fact]
    public void OverReturnedReadIsNotCreditedAsDataOrSourceCategory()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("read", 2, 71, requested: 1, count: 2, category: "f9e0_candidate"));
        Assert.Contains("Reads observed / successful with data: 1 / 0", report);
        Assert.Contains("Inconsistent TX / RX batch counts (returned > requested): 0 / 1", report);
        Assert.Contains("RX candidates E3-02 / F9E0 / F9F0 / D2-fragment: 0 / 0 / 0 / 0", report);
    }

    [Fact]
    public void NonzeroReadWithZeroRequestIsUntrusted()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("read", 2, 71, requested: 0, count: 1));
        Assert.Contains("Reads observed / successful with data: 1 / 0", report);
        Assert.Contains("Inconsistent TX / RX batch counts (returned > requested): 0 / 1", report);
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
    }

    [Fact]
    public void InconsistentReadInvalidatesEarlierAcceptedTransmitWindow()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 2, 71, requested: 1, count: 1),
            Event("read", 3, 71, requested: 1, count: 2),
            Event("read", 4, 71, requested: 1, count: 1));
        Assert.Contains("Inconsistent TX / RX batch counts (returned > requested): 0 / 1", report);
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 1", report);
    }

    [Fact]
    public void NewValidWriteAfterAnomalyRestoresOnlyNewWindow()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 2, 71, requested: 1, count: 1),
            Event("read", 3, 71, requested: 1, count: 2),
            Event("write", 4, 71, requested: 1, count: 1),
            Event("read", 5, 71, requested: 1, count: 1));
        Assert.Contains("Inconsistent TX / RX batch counts (returned > requested): 0 / 1", report);
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 0", report);
    }

    [Fact]
    public void InconsistentBatchIsChannelScopedAndReportIsRedacted()
    {
        var report = Inspect(
            Event("connect", 1, 7654321),
            Event("connect", 2, 76),
            Event("write", 3, 7654321, requested: 1, count: 1),
            Event("write", 4, 76, requested: 1, count: 2),
            Event("read", 5, 7654321, requested: 1, count: 1),
            Event("read", 6, 76, requested: 1, count: 1));
        Assert.Contains("Inconsistent TX / RX batch counts (returned > requested): 1 / 0", report);
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 1", report);
        Assert.Contains("not ECU evidence", report);
        Assert.DoesNotContain("7654321", report);
        Assert.DoesNotContain("0x0BADF00D", report);
        Assert.DoesNotContain("b9fb_tx", report);
    }

    private static string Inspect(params string[] events) =>
        J2534OfflineTraceInspection.BuildRedactedReport(string.Join("\n", events));

    private static string Event(string kind, uint ms, uint channel, uint status = 0,
        uint requested = 1, uint count = 0, string category = "other") =>
        JsonSerializer.Serialize(new
        {
            schema = 15, ms, @event = kind, channel, status, requested, count,
            @class = category, can_id = "0x0BADF00D", size = 0, value = 0,
            core_version = 26
        });
}
