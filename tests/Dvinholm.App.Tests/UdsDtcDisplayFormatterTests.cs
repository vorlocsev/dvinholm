namespace Dvinholm.App.Tests;

public sealed class UdsDtcDisplayFormatterTests
{
    [Fact]
    public void Format_EmptyResponseStatesRequestedMaskWithoutClaimingFaultFreeVehicle()
    {
        var text = Dvinholm.App.UdsDtcDisplayFormatter.Format(
            Array.Empty<Dvinholm.App.UdsDtcRecord>(), 0xFF);

        Assert.Contains("0 DTC records returned", text);
        Assert.Contains("status mask 0xFF", text);
        Assert.Contains("does not confirm", text);
        Assert.Contains("fault-free", text);
        Assert.DoesNotContain("No errors", text);
    }

    [Fact]
    public void Format_OneDtcShowsFullCodeAndReadableStatus()
    {
        var items = new[] { new Dvinholm.App.UdsDtcRecord(0x123456, 0x08) };

        var text = Dvinholm.App.UdsDtcDisplayFormatter.Format(items);

        Assert.Contains("1 DTC record returned", text);
        Assert.Contains("DTC 0x123456 | status 0x08: confirmed DTC", text);
        Assert.DoesNotContain("P1234", text);
    }

    [Fact]
    public void Format_MultipleRecordsAppearOnSeparateLinesInEcuOrder()
    {
        var items = new[]
        {
            new Dvinholm.App.UdsDtcRecord(0xABCDEF, 0x0D),
            new Dvinholm.App.UdsDtcRecord(0x000007, 0x80)
        };

        var text = Dvinholm.App.UdsDtcDisplayFormatter.Format(items, 0x04);
        var lines = text.Split(Environment.NewLine);

        Assert.Equal(3, lines.Length);
        Assert.Contains("2 DTC records returned (requested status mask 0x04)", lines[0]);
        Assert.StartsWith("DTC 0xABCDEF | status 0x0D:", lines[1]);
        Assert.Contains("test failed, pending DTC, confirmed DTC", lines[1]);
        Assert.Equal("DTC 0x000007 | status 0x80: warning indicator requested", lines[2]);
    }

    [Fact]
    public void Format_ZeroStatusExplicitlyStatesNoBitsSet()
    {
        var items = new[] { new Dvinholm.App.UdsDtcRecord(0x123456, 0) };

        var text = Dvinholm.App.UdsDtcDisplayFormatter.Format(items);

        Assert.Contains("DTC 0x123456 | status 0x00: no status bits set", text);
    }

    [Fact]
    public void Format_PreservesDuplicateRecordsInsteadOfSilentlyDroppingEvidence()
    {
        var items = new[]
        {
            new Dvinholm.App.UdsDtcRecord(0x123456, 0x04),
            new Dvinholm.App.UdsDtcRecord(0x123456, 0x08)
        };

        var lines = Dvinholm.App.UdsDtcDisplayFormatter.Format(items).Split(Environment.NewLine);

        Assert.Equal(3, lines.Length);
        Assert.Contains("pending DTC", lines[1]);
        Assert.Contains("confirmed DTC", lines[2]);
    }

    [Fact]
    public void Format_UsesExplicitZeroMaskWithoutAssumingTheEntireVehicleWasQueried()
    {
        var text = Dvinholm.App.UdsDtcDisplayFormatter.Format(
            Array.Empty<Dvinholm.App.UdsDtcRecord>(), 0);

        Assert.Contains("status mask 0x00", text);
        Assert.Contains("vehicle", text);
    }

    [Fact]
    public void Format_NullRecordsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Dvinholm.App.UdsDtcDisplayFormatter.Format(null!));
    }

    [Fact]
    public void Format_DoesNotInferManufacturerSpecificDescription()
    {
        var items = new[] { new Dvinholm.App.UdsDtcRecord(0xAB0001, 0x20) };
        var text = Dvinholm.App.UdsDtcDisplayFormatter.Format(items);

        Assert.Contains("DTC 0xAB0001 | status 0x20: failed since last clear", text);
        Assert.DoesNotContain("sensor", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ECM", text);
    }
}
