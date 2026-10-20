namespace Dvinholm.App.Tests;

public sealed class CanDemoFrameGeneratorTests
{
    [Fact]
    public void SyntheticBatchIsDeterministicAndContainsExpectedCanIds()
    {
        var first = Dvinholm.App.CanDemoFrameGenerator.CreateBatch(0);
        var replay = Dvinholm.App.CanDemoFrameGenerator.CreateBatch(0);

        Assert.Equal(first, replay);
        Assert.Equal(new[] { "7E0", "7E8", "18DAF110", "000" },
            first.Select(row => row.Id));
        Assert.Equal(new[] { "11-bit", "11-bit", "29-bit", "11-bit" },
            first.Select(row => row.Type));
    }

    [Fact]
    public void EverySyntheticRowHasConsistentClassicCanDlc()
    {
        foreach (var row in Dvinholm.App.CanDemoFrameGenerator.CreateBatch(15))
        {
            Assert.InRange(row.Dlc, 0u, 8u);
            Assert.Equal((int)row.Dlc,
                row.Data.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.All(row.Data.Split(' '),
                token => Assert.True(byte.TryParse(token,
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out _)));
        }
    }

    [Fact]
    public void LaterTickAdvancesTimestampAndPayloadMarker()
    {
        var first = Dvinholm.App.CanDemoFrameGenerator.CreateBatch(0);
        var second = Dvinholm.App.CanDemoFrameGenerator.CreateBatch(1);

        Assert.Equal((uint)0, first[0].Timestamp);
        Assert.Equal((uint)200, second[0].Timestamp);
        Assert.NotEqual(first[0].Data, second[0].Data);
    }

    [Fact]
    public void DemoRowsUseExistingHistoryAndFilterWithoutExternalAdapter()
    {
        var history = new Dvinholm.App.CanMonitorHistory();
        foreach (var row in Dvinholm.App.CanDemoFrameGenerator.CreateBatch(0))
            history.Append(row);
        foreach (var row in Dvinholm.App.CanDemoFrameGenerator.CreateBatch(1))
            history.Append(row);

        Assert.Equal(8, history.TotalReceived);
        Assert.Equal(2, history.Filter("7E8").Count);
        Assert.Equal(8, history.Filter("").Count);
        Assert.Equal(2, history.Filter("0").Count);
        history.Clear();
        Assert.Empty(history.Filter(""));
    }

    [Fact]
    public void DemoCsvExplicitlyMarksEverySyntheticRow()
    {
        var rows = Dvinholm.App.CanDemoFrameGenerator.CreateBatch(7);
        var lines = Dvinholm.App.CanMonitorCsv.BuildLines(rows, syntheticDemo: true);

        Assert.Equal("timestamp,id,type,dlc,data,source", lines[0]);
        Assert.Equal(rows.Count + 1, lines.Count);
        Assert.All(lines.Skip(1), line => Assert.EndsWith(",SYNTHETIC_DEMO", line));
        Assert.Contains(",18DAF110,29-bit,5,", lines[3]);
    }

    [Fact]
    public void RealCsvRetainsOriginalFiveColumnSchema()
    {
        var rows = Dvinholm.App.CanDemoFrameGenerator.CreateBatch(2).Take(1);
        var lines = Dvinholm.App.CanMonitorCsv.BuildLines(rows, syntheticDemo: false);

        Assert.Equal("timestamp,id,type,dlc,data", lines[0]);
        Assert.Equal(2, lines.Count);
        Assert.DoesNotContain("SYNTHETIC", string.Join("\n", lines));
        Assert.Equal("400,7E0,11-bit,4,\"A5 5A 02 01\"", lines[1]);
    }

    [Fact]
    public void ExportOnlyTheRowsVisibleUnderSoftwareFilter()
    {
        var history = new Dvinholm.App.CanMonitorHistory();
        foreach (var row in Dvinholm.App.CanDemoFrameGenerator.CreateBatch(0))
            history.Append(row);

        var lines = Dvinholm.App.CanMonitorCsv.BuildLines(
            history.Filter("7E8"), syntheticDemo: true);

        Assert.Equal(2, lines.Count);
        Assert.Contains(",7E8,11-bit,", lines[1]);
        Assert.DoesNotContain(",7E0,", lines[1]);
    }

    [Fact]
    public void EmptyDemoExportRetainsSyntheticSourceHeader()
    {
        var lines = Dvinholm.App.CanMonitorCsv.BuildLines(
            Array.Empty<Dvinholm.App.CanMonitorRow>(), syntheticDemo: true);

        Assert.Single(lines);
        Assert.Equal("timestamp,id,type,dlc,data,source", lines[0]);
    }
}
