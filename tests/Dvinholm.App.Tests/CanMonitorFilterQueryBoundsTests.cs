namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline regressions for bounded CAN Monitor software filter input.
/// This is a display-only filter; no J2534 hardware filter is changed.
/// </summary>
public sealed class CanMonitorFilterQueryBoundsTests
{
    private static CanMonitorRow Row(string id, uint timestamp = 1) =>
        new(timestamp, id, "11-bit", 1, "01");

    [Fact]
    public void OversizedPastedQueryFailsClosedWithoutClearingHistory()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("7E8"));
        var oversized = new string(' ', CanMonitorHistory.MaxSoftwareFilterCharacters) + "7E8";

        Assert.Empty(history.Filter(oversized));
        Assert.False(CanMonitorHistory.MatchesCanIdFilter(oversized, "7E8"));
        Assert.Single(history.Filter("7E8"));
        Assert.Equal(1, history.TotalReceived);
    }

    [Fact]
    public void OversizedWhitespaceOnlyQueryIsNotAnUnlimitedWildcard()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("7E8"));
        var oversized = new string(' ', CanMonitorHistory.MaxSoftwareFilterCharacters + 1);

        Assert.Empty(history.Filter(oversized));
        Assert.False(CanMonitorHistory.MatchesCanIdFilter(oversized, "7E8"));
        Assert.Single(history.Filter("  "));
    }

    [Fact]
    public void NineHexDigitsWithLeadingZerosAreRejected()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("000"));
        Assert.Empty(history.Filter("000000000"));
        Assert.Empty(history.Filter("0x000000000"));
        Assert.False(CanMonitorHistory.MatchesCanIdFilter("0x000000000", "000"));
        Assert.Single(history.Filter("0"));
    }

    [Fact]
    public void EightDigitZeroPaddedIdentifierStillMatchesShortId()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("000007E8"));
        history.Append(Row("7E0", 2));

        Assert.Equal(new uint[] { 1 }, history.Filter("0x000007e8").Select(x => x.Timestamp));
        Assert.True(CanMonitorHistory.MatchesCanIdFilter("7E8", "000007E8"));
        Assert.Equal(2, history.BufferedCount);
    }

    [Fact]
    public void SixteenPrefixedFullWidthCanIdsFitWithinQueryLimit()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("00F"));
        var query = string.Join(",", Enumerable.Range(0, CanMonitorHistory.MaxSoftwareFilterIds)
            .Select(i => $"0x{i:X8}"));

        Assert.True(query.Length <= CanMonitorHistory.MaxSoftwareFilterCharacters);
        Assert.Single(history.Filter(query));
        Assert.Single(history.Filter("00F"));
        Assert.Empty(history.Filter(query + ",0x00000010"));
        Assert.Equal(1, history.TotalReceived);
    }

    [Fact]
    public void HighestValid29BitIdMatchesButOutOfRangeDoesNot()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("1FFFFFFF"));

        Assert.Single(history.Filter("0X1fffffff"));
        Assert.True(CanMonitorHistory.MatchesCanIdFilter("1FFFFFFF", "1FFFFFFF"));
        Assert.Empty(history.Filter("0X20000000"));
        Assert.False(CanMonitorHistory.MatchesCanIdFilter("20000000", "1FFFFFFF"));
    }

    [Fact]
    public void MalformedStoredIdCannotMatchWhileBlankFilterRemainsWildcard()
    {
        var history = new CanMonitorHistory();
        history.Append(Row("0000007E8"));
        history.Append(Row("7E8", 2));

        Assert.Equal(new uint[] { 2 }, history.Filter("7E8").Select(x => x.Timestamp));
        Assert.Equal(2, history.Filter(" ").Count);
        Assert.True(CanMonitorHistory.MatchesCanIdFilter(null, "invalid-id"));
        Assert.False(CanMonitorHistory.MatchesCanIdFilter("7E8", "0000007E8"));
    }

    [Fact]
    public void VirtualJ2534ReceiveSurvivesInvalidFilterWithoutAnyTransmit()
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

        Assert.Empty(history.Filter(new string('0', 4096)));
        Assert.Equal(new[] { "7E8", "18DAF110" },
            history.Filter("0x7E8,0x18DAF110").Select(x => x.Id));
        Assert.Equal(2, history.TotalReceived);
        Assert.Equal(0, adapter.TransmittedFrames);
    }
}
