namespace Dvinholm.App.Tests;

public sealed class UdsDtcStatusFormatterTests
{
    [Fact]
    public void Format_ZeroStatusReportsNoFlags()
    {
        Assert.Equal("no status bits set", Dvinholm.App.UdsDtcStatusFormatter.Format(0x00));
    }

    [Theory]
    [InlineData(0x01, "test failed")]
    [InlineData(0x02, "failed this operation cycle")]
    [InlineData(0x04, "pending DTC")]
    [InlineData(0x08, "confirmed DTC")]
    [InlineData(0x10, "test not completed since last clear")]
    [InlineData(0x20, "failed since last clear")]
    [InlineData(0x40, "test not completed this operation cycle")]
    [InlineData(0x80, "warning indicator requested")]
    public void Format_DescribesEachIndividualStatusBit(byte status, string expected)
    {
        Assert.Equal(expected, Dvinholm.App.UdsDtcStatusFormatter.Format(status));
    }

    [Fact]
    public void Format_DescribesCombinedFlagsInStableBitOrder()
    {
        Assert.Equal(
            "test failed, pending DTC, confirmed DTC",
            Dvinholm.App.UdsDtcStatusFormatter.Format(0x0D));
    }

    [Fact]
    public void Format_AllBitsProducesEveryStandardDescription()
    {
        var formatted = Dvinholm.App.UdsDtcStatusFormatter.Format(0xFF);

        Assert.Contains("test failed", formatted);
        Assert.Contains("confirmed DTC", formatted);
        Assert.Contains("test not completed since last clear", formatted);
        Assert.Contains("warning indicator requested", formatted);
        Assert.Equal(8, formatted.Split(", ").Length);
    }
}
