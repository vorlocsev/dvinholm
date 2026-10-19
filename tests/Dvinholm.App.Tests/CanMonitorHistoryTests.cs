namespace Dvinholm.App.Tests;

public sealed class CanMonitorHistoryTests
{
    private static Dvinholm.App.CanMonitorRow Row(string id, uint timestamp) =>
        new(timestamp, id, "11-bit", 2, "01 02");

    [Fact]
    public void ChangingFilterRestoresPreviouslyHiddenFrames()
    {
        var history = new Dvinholm.App.CanMonitorHistory();
        history.Append(Row("7E0", 1));
        history.Append(Row("7E8", 2));
        history.Append(Row("123", 3));

        Assert.Equal(new uint[] { 2 }, history.Filter("0x7e8").Select(r => r.Timestamp));
        Assert.Equal(new uint[] { 1, 2, 3 },
            history.Filter("").Select(r => r.Timestamp));
        Assert.Equal(new uint[] { 1 }, history.Filter("7E0").Select(r => r.Timestamp));
        Assert.Equal(3, history.TotalReceived);
    }

    [Fact]
    public void InvalidFilterShowsNoFramesWithoutDeletingBufferedFrames()
    {
        var history = new Dvinholm.App.CanMonitorHistory();
        history.Append(Row("7E8", 5));

        Assert.Empty(history.Filter("invalid!"));
        Assert.Single(history.Filter("7E8"));
        Assert.Equal(1, history.BufferedCount);
        Assert.Equal(1, history.TotalReceived);
    }

    [Fact]
    public void ZeroIdMatchesOnlyZeroAndNotEmptyWildcard()
    {
        var history = new Dvinholm.App.CanMonitorHistory();
        history.Append(Row("000", 1));
        history.Append(Row("001", 2));

        Assert.Single(history.Filter("0"));
        Assert.Equal(2, history.Filter(" ").Count);
    }

    [Fact]
    public void BufferEvictionIsIndependentOfTheCurrentFilter()
    {
        var history = new Dvinholm.App.CanMonitorHistory(3);
        var oldest = Row("7E0", 1);
        Assert.Null(history.Append(oldest));
        history.Append(Row("7E8", 2));
        history.Append(Row("7E0", 3));
        var evicted = history.Append(Row("123", 4));

        Assert.Same(oldest, evicted);
        Assert.Equal(new uint[] { 2, 3, 4 },
            history.Filter("").Select(r => r.Timestamp));
        Assert.Equal(new uint[] { 3 }, history.Filter("7E0").Select(r => r.Timestamp));
        Assert.Equal(4, history.TotalReceived);
        Assert.Equal(3, history.BufferedCount);
    }

    [Fact]
    public void ClearResetsBufferAndReceivedCount()
    {
        var history = new Dvinholm.App.CanMonitorHistory(2);
        history.Append(Row("7E8", 1));
        history.Clear();

        Assert.Empty(history.Filter(""));
        Assert.Equal(0, history.TotalReceived);
        Assert.Equal(0, history.BufferedCount);
        Assert.Null(history.Append(Row("7E0", 2)));
        Assert.Equal(1, history.TotalReceived);
    }

    [Fact]
    public void RejectsInvalidCapacityAndNullRows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Dvinholm.App.CanMonitorHistory(0));
        var history = new Dvinholm.App.CanMonitorHistory();
        Assert.Throws<ArgumentNullException>(() => history.Append(null!));
    }

    [Fact]
    public void VirtualAdapterReceiveAndFilterChangePreserveOrderAndFrameDetails()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        using var monitor = bus.Subscribe();
        bus.Start();

        var injected = new[]
        {
            new Dvinholm.App.CanFrame(0x7E0, false, false, new byte[] { 0x01, 0x22 }, 0, 0),
            new Dvinholm.App.CanFrame(0x7E8, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x90 }, 0, 0),
            new Dvinholm.App.CanFrame(0x7E0, false, false, Array.Empty<byte>(), 0, 0)
        };
        foreach (var frame in injected)
            adapter.InjectReceiveFrame(frame);

        var history = new Dvinholm.App.CanMonitorHistory();
        foreach (var _ in injected)
        {
            Assert.True(monitor.TryRead(TimeSpan.FromSeconds(2), CancellationToken.None,
                out var received), "Virtual adapter RX frame did not reach monitor.");
            history.Append(Dvinholm.App.CanMonitorRow.FromFrame(received));
        }

        var responses = history.Filter("7E8");
        Assert.Single(responses);
        Assert.Equal("03 62 F1 90", responses[0].Data);
        Assert.Equal((uint)4, responses[0].Dlc);

        var all = history.Filter("");
        Assert.Equal(new[] { "7E0", "7E8", "7E0" }, all.Select(r => r.Id));
        Assert.Equal((uint)0, all[2].Dlc);
        Assert.Equal(string.Empty, all[2].Data);
        Assert.Equal(3, history.TotalReceived);
        Assert.Equal(0, adapter.TransmittedFrames);
    }
}
