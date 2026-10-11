namespace Dvinholm.App.Tests;

public sealed class UdsDtcParserBoundaryTests
{
    [Fact]
    public void ParseDtcResponse_AcceptsEmptyDtcList()
    {
        var records = Dvinholm.App.UdsClient.ParseDtcResponse(new byte[] { 0x02, 0xFF });

        Assert.Empty(records);
    }

    [Fact]
    public void ParseDtcResponse_RejectsMissingSubfunction()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Dvinholm.App.UdsClient.ParseDtcResponse(Array.Empty<byte>()));

        Assert.Contains("subfunction is missing", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseDtcResponse_RejectsUnexpectedSubfunction()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Dvinholm.App.UdsClient.ParseDtcResponse(new byte[] { 0x01, 0xFF }));

        Assert.Contains("expected 0x02", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseDtcResponse_RejectsIncompleteHeader()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Dvinholm.App.UdsClient.ParseDtcResponse(new byte[] { 0x02 }));

        Assert.Contains("header is incomplete", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0x02, 0xFF, 0x12)]
    [InlineData(0x02, 0xFF, 0x12, 0x34)]
    [InlineData(0x02, 0xFF, 0x12, 0x34, 0x56)]
    [InlineData(0x02, 0xFF, 0x12, 0x34, 0x56, 0x2A, 0x99)]
    public void ParseDtcResponse_RejectsPartialDtcRecord(params byte[] response)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Dvinholm.App.UdsClient.ParseDtcResponse(response));

        Assert.Contains("record length", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseDtcResponse_ParsesMultipleRecordsWithoutLosingStatusBytes()
    {
        var records = Dvinholm.App.UdsClient.ParseDtcResponse(new byte[]
        {
            0x02, 0xFF,
            0x12, 0x34, 0x56, 0x2A,
            0xAB, 0xCD, 0xEF, 0x80
        });

        Assert.Collection(
            records,
            first =>
            {
                Assert.Equal((uint)0x123456, first.Code);
                Assert.Equal((byte)0x2A, first.Status);
            },
            second =>
            {
                Assert.Equal((uint)0xABCDEF, second.Code);
                Assert.Equal((byte)0x80, second.Status);
            });
    }
}
