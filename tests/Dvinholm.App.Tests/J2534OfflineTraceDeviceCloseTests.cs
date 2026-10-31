using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight isolated, metadata-only checks for the J2534 device close boundary.
/// The proxy logs device handles on close and channel handles on connect.
/// It does NOT log the relationship, so successful close is global fail-closed.
/// </summary>
public sealed class J2534OfflineTraceDeviceCloseTests
{
    [Fact]
    public void SuccessfulCloseRevokesExistingWriteAndOrphansOldChannel()
    {
        var report = Inspect(
            Event("connect", 10, 71),
            Event("write", 11, 71, count: 1, category: "a3_tx"),
            Event("close", 12, 200),
            Event("read", 13, 71, count: 1));
        Assert.Contains("Successful / failed device closes: 1 / 0", report);
        Assert.Contains("Channel observations / TX windows invalidated by device close: 1 / 1", report);
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("Orphan channel calls / other events: 1 / 1", report);
    }

    [Fact]
    public void FailedClosePreservesAcceptedWriteWindow()
    {
        var report = Inspect(
            Event("connect", 10, 71),
            Event("write", 11, 71, count: 1),
            Event("close", 12, 200, status: 7),
            Event("read", 13, 71, count: 1));
        Assert.Contains("Successful / failed device closes: 0 / 1", report);
        Assert.Contains("Channel observations / TX windows invalidated by device close: 0 / 0", report);
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
    }

    [Fact]
    public void CloseWithoutDeviceChannelMappingInvalidatesAllChannels()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("connect", 2, 72),
            Event("write", 3, 71, count: 1),
            Event("write", 4, 72, count: 1),
            Event("close", 5, 200),
            Event("read", 6, 71, count: 1),
            Event("read", 7, 72, count: 1));
        Assert.Contains("Channel observations / TX windows invalidated by device close: 2 / 2", report);
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("Orphan channel calls / other events: 2 / 1", report);
    }

    [Fact]
    public void ReconnectedSameChannelRequiresNewSuccessfulWrite()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 2, 71, count: 1),
            Event("close", 3, 200),
            Event("open", 4, 200),
            Event("connect", 5, 71),
            Event("read", 6, 71, count: 1),
            Event("write", 7, 71, count: 1),
            Event("read", 8, 71, count: 1));
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 1", report);
        Assert.Contains("Successful / failed device closes: 1 / 0", report);
    }

    [Fact]
    public void OpenAndIoctlAloneDoNotClaimDeviceClose()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 2, 71, count: 1),
            Event("open", 3, 200),
            Event("ioctl", 4, 71),
            Event("read", 5, 71, count: 1));
        Assert.Contains("Successful / failed device closes: 0 / 0", report);
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
        Assert.Contains("Orphan channel calls / other events: 0 / 2", report);
    }

    [Fact]
    public void RepeatedClosesCannotDoubleCountRevokedChannels()
    {
        var report = Inspect(
            Event("connect", 1, 71),
            Event("write", 2, 71, count: 1),
            Event("close", 3, 200),
            Event("close", 4, 200),
            Event("close", 5, 200, status: 7));
        Assert.Contains("Successful / failed device closes: 2 / 1", report);
        Assert.Contains("Channel observations / TX windows invalidated by device close: 1 / 1", report);
        Assert.Contains("Orphan channel calls / other events: 0 / 3", report);
    }

    [Fact]
    public void ClockRegressionAfterCloseDoesNotRestorePriorWindow()
    {
        var report = Inspect(
            Event("connect", 10, 71),
            Event("write", 11, 71, count: 1),
            Event("close", 12, 200),
            Event("connect", 13, 71),
            Event("read", 14, 71, count: 1),
            Event("read", 1, 71, count: 1),
            Event("connect", 2, 71),
            Event("read", 3, 71, count: 1));
        Assert.Contains("Timestamp regressions/wraps: 1; invalidated TX windows: 0", report);
        Assert.Contains("Channel observations / TX windows invalidated by device close: 1 / 1", report);
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 2", report);
        Assert.Contains("Orphan channel calls / other events: 1 / 1", report);
    }

    [Fact]
    public void CloseBoundaryReportRemainsRedactedAndNonAuthoritative()
    {
        var report = Inspect(
            Event("connect", 1, 7654321),
            Event("write", 2, 7654321, count: 1, category: "b9fb_tx"),
            Event("close", 3, 999888));
        Assert.Contains("device-to-channel mapping is absent", report);
        Assert.Contains("NOT verified ECU response", report);
        Assert.DoesNotContain("7654321", report);
        Assert.DoesNotContain("999888", report);
        Assert.DoesNotContain("0x0BADF00D", report);
        Assert.DoesNotContain("b9fb_tx", report);
    }

    private static string Inspect(params string[] events) =>
        J2534OfflineTraceInspection.BuildRedactedReport(string.Join("\n", events));

    private static string Event(string kind, uint ms, uint channel, uint status = 0,
        uint count = 0, string category = "other", uint requested = 1) =>
        JsonSerializer.Serialize(new
        {
            schema = 15, ms, @event = kind, channel, status, requested, count,
            @class = category, can_id = "0x0BADF00D", size = 0, value = 0,
            core_version = 26
        });
}
