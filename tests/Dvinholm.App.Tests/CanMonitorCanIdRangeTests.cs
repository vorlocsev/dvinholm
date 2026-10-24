namespace Dvinholm.App.Tests;

/// <summary>Eight CAN 2.0 arbitration-range checks for the UI-only software filter.</summary>
public sealed class CanMonitorCanIdRangeTests
{
    private static CanMonitorRow Row(string id, uint timestamp) =>
        new(timestamp, id, "29-bit", 1, "55");

    [Fact]
    public void HighestValid29BitIdMatchesWithHexPrefix()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("1FFFFFFF", 1));
        Assert.Single(history.Filter("0x1fffffff"));
        Assert.True(CanMonitorHistory.MatchesCanIdFilter("1FFFFFFF", "1fffffff"));
    }

    [Fact]
    public void FirstOutOfRange29BitIdIsRejected()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("1FFFFFFF", 1));
        Assert.Empty(history.Filter("20000000"));
        Assert.False(CanMonitorHistory.MatchesCanIdFilter("0x20000000", "20000000"));
        Assert.Single(history.Filter("1FFFFFFF"));
    }

    [Fact]
    public void MaximumUintIsNotAValidCanArbitrationId()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("FFFFFFFF", 1));
        Assert.Empty(history.Filter("FFFFFFFF"));
        Assert.False(CanMonitorHistory.MatchesCanIdFilter("0xffffffff", "FFFFFFFF"));
        Assert.Equal(1, history.TotalReceived);
    }

    [Fact]
    public void WiderThanUintInputFailsClosedInsteadOfWrapping()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("000", 1));
        Assert.Empty(history.Filter("100000000"));
        Assert.False(CanMonitorHistory.MatchesCanIdFilter("100000000", "000"));
        Assert.Single(history.Filter("0"));
    }

    [Fact]
    public void OutOfRangeMemberRejectsWholeListButKeepsHistory()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("7E0", 1));
        history.Append(Row("18DAF110", 2));
        history.Append(Row("7E8", 3));

        Assert.Empty(history.Filter("7E0, 20000000, 7E8"));
        Assert.False(CanMonitorHistory.MatchesCanIdFilter("7E0,20000000", "7E0"));
        Assert.Equal(new uint[] { 1, 2, 3 },
            history.Filter(null).Select(row => row.Timestamp));
        Assert.Equal(3, history.TotalReceived);
    }

    [Fact]
    public void EvenMalformedStoredRowCannotMatchValidCanIdFilter()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("FFFFFFFF", 1));
        history.Append(Row("1FFFFFFF", 2));
        Assert.Equal(new uint[] { 2 },
            history.Filter("1FFFFFFF").Select(row => row.Timestamp));
        Assert.False(CanMonitorHistory.MatchesCanIdFilter("1FFFFFFF", "FFFFFFFF"));
    }

    [Fact]
    public void LegacyNumeric11BitMatchingAndBlankWildcardStayCompatible()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("000007E8", 1));
        history.Append(Row("7E0", 2));
        Assert.Equal(new uint[] { 1 }, history.Filter("0X7e8").Select(r => r.Timestamp));
        Assert.True(CanMonitorHistory.MatchesCanIdFilter("7E8", "000007E8"));
        Assert.Equal(2, history.Filter("  ").Count);
        // The empty query remains a wildcard even when upstream display data is malformed.
        Assert.True(CanMonitorHistory.MatchesCanIdFilter("", "not-an-id"));
    }

    [Fact]
    public void VirtualAdapterValidMixedFramesStillFilterWithoutTransmitting()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        using var monitor = bus.Subscribe();
        bus.Start();

        adapter.InjectReceiveFrame(new CanFrame(0x7E8, false, false, [0x01], 1, 0));
        adapter.InjectReceiveFrame(new CanFrame(0x18DAF110, true, false, [0x02], 2, 0));

        var history = new CanMonitorHistory();
        for (var i = 0; i < 2; i++)
        {
            Assert.True(monitor.TryRead(TimeSpan.FromSeconds(2),
                CancellationToken.None, out var received));
            history.Append(CanMonitorRow.FromFrame(received));
        }

        Assert.Equal(new[] { "7E8", "18DAF110" },
            history.Filter("0x7e8,0x18daf110").Select(r => r.Id));
        Assert.Empty(history.Filter("7E8,FFFFFFFF"));
        Assert.Equal(2, history.TotalReceived);
        Assert.Equal(0, adapter.TransmittedFrames);
    }
}
