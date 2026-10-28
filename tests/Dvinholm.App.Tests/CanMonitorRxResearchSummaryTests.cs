namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic, synthetic-only checks for the read-only CAN RX
/// metadata summary. No physical DiCE/J2534 adapter or P2 CEM protocol.
/// </summary>
public sealed class CanMonitorRxResearchSummaryTests
{
    [Fact]
    public void EmptyBufferProducesClearUnverifiedOfflineReport()
    {
        var report = CanMonitorRxResearchSummary.Build(
            [], totalReceivedSinceClear: 0,
            subscriberDroppedFrames: null, syntheticDemo: false);

        Assert.Contains("Rows retained in bounded history: 0", report);
        Assert.Contains("UNKNOWN (no active subscription)", report);
        Assert.Contains("NOT authenticated", report);
        Assert.Contains("No CAN transmission", report);
    }

    [Fact]
    public void DuplicateFramesAreCountedAndDlcHistogramIsDeterministic()
    {
        var report = CanMonitorRxResearchSummary.Build(
            [
                Row("7E8", "11-bit", 2),
                Row("7E0", "11-bit", 8),
                Row("7E8", "11-bit", 0),
                Row("7E8", "11-bit", 2)
            ], 4, 0, false);

        Assert.Contains("CAN 11-bit 7E8: 3 row(s), DLC 0=1, 2=2", report);
        Assert.Contains("CAN 11-bit 7E0: 1 row(s), DLC 8=1", report);
        Assert.True(report.IndexOf("CAN 11-bit 7E0", StringComparison.Ordinal) <
                    report.IndexOf("CAN 11-bit 7E8", StringComparison.Ordinal));
        Assert.Contains("RX subscriber reported drops: 0", report);
    }

    [Fact]
    public void IdenticalNumericCanIdFromDifferentFormatsRemainsSeparate()
    {
        var report = CanMonitorRxResearchSummary.Build(
            [
                Row("000007E8", "29-bit", 3),
                Row("7E8", "11-bit", 3)
            ], 2, null, false);

        Assert.Contains("Distinct CAN ID + format groups: 2", report);
        Assert.Contains("CAN 11-bit 7E8: 1 row(s)", report);
        Assert.Contains("CAN 29-bit 000007E8: 1 row(s)", report);
    }

    [Fact]
    public void ReportingUsesCompleteHistoryInsteadOfCurrentSoftwareFilter()
    {
        var history = new CanMonitorHistory(3);
        history.Append(Row("7E0", "11-bit", 2));
        history.Append(Row("7E8", "11-bit", 4));
        history.Append(Row("123", "11-bit", 8));

        Assert.Single(history.Filter("7E8"));

        var report = CanMonitorRxResearchSummary.Build(
            history.Filter(""), history.TotalReceived,
            subscriberDroppedFrames: 1, syntheticDemo: false);

        Assert.Contains("CAN 11-bit 7E0", report);
        Assert.Contains("CAN 11-bit 7E8", report);
        Assert.Contains("CAN 11-bit 123", report);
        Assert.Contains("RX subscriber reported drops: 1", report);
    }

    [Fact]
    public void PayloadIsNeverIncludedEvenWhenItLooksLikeVinOrSecret()
    {
        const string secret = "YV1CM59H271234567 PIN 54321";
        var report = CanMonitorRxResearchSummary.Build(
            [
                new CanMonitorRow(100, "7E8", "11-bit", 8, secret),
                new CanMonitorRow(200, "7E0", "11-bit", 2, "DE AD")
            ], 2, 0, false);

        Assert.DoesNotContain(secret, report);
        Assert.DoesNotContain("DE AD", report);
        Assert.DoesNotContain("100", report);
        Assert.DoesNotContain("200", report);
        Assert.Contains("No frame payload", report);
    }

    [Fact]
    public void InvalidInputFailsClosedInsteadOfEchoingUntrustedMetadata()
    {
        var invalid = new[]
        {
            Row("800", "11-bit", 2),
            Row("7E8", "invalid-type", 2),
            Row("7e8", "11-bit", 2),
            Row("7E8", "11-bit", 9),
            Row("20000000", "29-bit", 8),
            Row("000007E8", "11-bit", 3)
        };
        foreach (var row in invalid)
            Assert.Throws<InvalidDataException>(() =>
                CanMonitorRxResearchSummary.Build([row], 1, null, false));

        Assert.Throws<InvalidDataException>(() =>
            CanMonitorRxResearchSummary.Build([Row("7E8", "11-bit", 2)], 0, 0, false));
        Assert.Throws<InvalidDataException>(() =>
            CanMonitorRxResearchSummary.Build([], 0, -1, false));
        Assert.Throws<InvalidDataException>(() =>
            CanMonitorRxResearchSummary.Build(
                Enumerable.Repeat(Row("123", "11-bit", 1),
                    CanMonitorHistory.DefaultCapacity + 1).ToArray(),
                CanMonitorHistory.DefaultCapacity + 1, null, false));
    }

    [Fact]
    public void ExcessGroupsAreBoundedAndSyntheticFramesAreClearlyMarked()
    {
        var rows = Enumerable.Range(0, CanMonitorRxResearchSummary.MaximumDisplayedIdGroups + 1)
            .Select(id => Row(id.ToString("X3"), "11-bit", 1))
            .ToArray();
        var report = CanMonitorRxResearchSummary.Build(
            rows, totalReceivedSinceClear: 600,
            subscriberDroppedFrames: null, syntheticDemo: true);

        Assert.Contains("Source: SYNTHETIC DEMO", report);
        Assert.Contains("Older rows no longer buffered: 535", report);
        Assert.Contains("Distinct CAN ID + format groups: 65", report);
        Assert.Contains("Additional groups withheld by bounded display: 1", report);
        Assert.Contains("CAN 11-bit 03F", report);
        Assert.DoesNotContain("CAN 11-bit 040:", report);
    }

    [Fact]
    public void VirtualJ2534ReceiveOnlyFlowGeneratesMetadataWithoutTransmitting()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        using var monitor = bus.Subscribe();
        bus.Start();

        adapter.InjectReceiveFrame(new CanFrame(
            0x7E8, false, false, [0x03, 0x62, 0xF1, 0x90],
            100, 0));
        adapter.InjectReceiveFrame(new CanFrame(
            0x7E0, false, false, [0x02, 0x10, 0x01],
            101, 0));

        var history = new CanMonitorHistory();
        for (var i = 0; i < 2; i++)
        {
            Assert.True(monitor.TryRead(TimeSpan.FromSeconds(2),
                CancellationToken.None, out var frame));
            history.Append(CanMonitorRow.FromFrame(frame));
        }

        var report = CanMonitorRxResearchSummary.Build(
            history.Filter(""), history.TotalReceived,
            subscriberDroppedFrames: monitor.DroppedFrameCount,
            syntheticDemo: false);
        Assert.Contains("CAN 11-bit 7E8: 1 row(s), DLC 4=1", report);
        Assert.Contains("CAN 11-bit 7E0: 1 row(s), DLC 3=1", report);
        Assert.DoesNotContain("03 62 F1 90", report);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    private static CanMonitorRow Row(string id, string type, uint dlc) =>
        new(1, id, type, dlc, "FA KE");
}
