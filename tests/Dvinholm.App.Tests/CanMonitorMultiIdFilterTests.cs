namespace Dvinholm.App.Tests;

public sealed class CanMonitorMultiIdFilterTests
{
    private static CanMonitorRow Row(string id, uint time) =>
        new(time, id, "11-bit", 2, "01 02");

    [Fact]
    public void LegacySingleId_AndHexPrefixStillMatch()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("7E0", 1));
        history.Append(Row("7E8", 2));
        Assert.Equal(new uint[] { 2 }, history.Filter("7E8").Select(x => x.Timestamp));
        Assert.Equal(new uint[] { 1 }, history.Filter(" 0X7e0 ").Select(x => x.Timestamp));
        Assert.True(CanMonitorHistory.MatchesCanIdFilter("0x7E8", "000007E8"));
    }

    [Fact]
    public void RequestAndResponseList_KeepOriginalArrivalOrder()
    {
        var history = new CanMonitorHistory();
        foreach (var row in new[]
        {
            Row("7E8", 1), Row("123", 2), Row("7E0", 3), Row("7E8", 4)
        }) history.Append(row);

        Assert.Equal(new uint[] { 1, 3, 4 },
            history.Filter("7E0,7E8").Select(x => x.Timestamp));
        Assert.Equal(4, history.TotalReceived);
        Assert.Equal(4, history.BufferedCount);
        Assert.Equal(new uint[] { 1, 2, 3, 4 },
            history.Filter("").Select(x => x.Timestamp));
    }

    [Fact]
    public void MixedPrefixed29BitAnd11BitIds_AreAccepted()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("18DAF110", 1));
        history.Append(Row("7E0", 2));
        history.Append(Row("123", 3));
        Assert.Equal(new uint[] { 1, 2 },
            history.Filter(" 0x7e0 , 0X18daf110 ").Select(x => x.Timestamp));
    }

    [Fact]
    public void RepeatedIdentifiers_DoNotDuplicateDisplayedFrames()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("7E0", 1));
        history.Append(Row("7E8", 2));
        Assert.Equal(new uint[] { 1, 2 },
            history.Filter("7E0,7E0,7E8,7E8").Select(x => x.Timestamp));
    }

    [Fact]
    public void InvalidMember_FailsEntireFilterWithoutErasingHistory()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("7E0", 1));
        history.Append(Row("7E8", 2));

        foreach (var text in new[] { "7E0,ZZZ", "7E0,0x0x7E8", "not-hex,7E0" })
        {
            Assert.Empty(history.Filter(text));
            Assert.False(CanMonitorHistory.MatchesCanIdFilter(text, "7E0"));
        }
        Assert.Equal(2, history.TotalReceived);
        Assert.Equal(2, history.Filter("7E0,7E8").Count);
    }

    [Fact]
    public void BlankMembers_FailClosedButBlankWholeFilterRemainsWildcard()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("7E0", 1));
        foreach (var text in new[] { ",7E0", "7E0,", "7E0,,7E8", "0x,7E0" })
            Assert.Empty(history.Filter(text));
        Assert.Single(history.Filter("  "));
        Assert.True(CanMonitorHistory.MatchesCanIdFilter(null, "not-an-id"));
    }

    [Fact]
    public void QueryLimit_AcceptsSixteenAndRejectsSeventeenWithoutDataLoss()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("00F", 1));
        var max = string.Join(",", Enumerable.Range(0, CanMonitorHistory.MaxSoftwareFilterIds)
            .Select(i => i.ToString("X3")));
        var overflow = max + ",010";
        Assert.Single(history.Filter(max));
        Assert.Empty(history.Filter(overflow));
        Assert.Equal(1, history.TotalReceived);
        Assert.Single(history.Filter("00F"));
    }

    [Fact]
    public void VirtualJ2534Feed_SoftwareListPreservesRxAndNeverTransmits()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        using var monitor = bus.Subscribe();
        bus.Start();

        var injected = new[]
        {
            new CanFrame(0x7E0, false, false, new byte[] { 0x01 }, 1, 0),
            new CanFrame(0x123, false, false, new byte[] { 0x02 }, 2, 0),
            new CanFrame(0x18DAF110, true, false, new byte[] { 0x03 }, 3, 0),
            new CanFrame(0x7E8, false, false, new byte[] { 0x04 }, 4, 0)
        };
        foreach (var frame in injected) adapter.InjectReceiveFrame(frame);

        var history = new CanMonitorHistory();
        foreach (var _ in injected)
        {
            Assert.True(monitor.TryRead(TimeSpan.FromSeconds(2), CancellationToken.None,
                out var received));
            history.Append(CanMonitorRow.FromFrame(received));
        }
        Assert.Equal(new[] { "7E0", "18DAF110", "7E8" },
            history.Filter("7E0,7E8,18DAF110").Select(x => x.Id));
        Assert.Equal(4, history.TotalReceived);
        Assert.Equal(0, adapter.TransmittedFrames);
    }
}
