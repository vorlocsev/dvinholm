using System.Text;

namespace Dvinholm.App.Tests;

public sealed class UdsDidDisplayFormatterTests
{
    [Fact]
    public void Format_ValidVinShowsHumanReadableVinAndOriginalBytes()
    {
        var bytes = new byte[19];
        bytes[0] = 0xF1;
        bytes[1] = 0x90;
        Encoding.ASCII.GetBytes("YV1ABCDEF12345678").CopyTo(bytes, 2);

        var text = Dvinholm.App.UdsDidDisplayFormatter.Format(0xF190, bytes);

        Assert.Contains("DID 0xF190", text);
        Assert.Contains("VIN): YV1ABCDEF12345678", text);
        Assert.Contains("raw: F1 90 59 56 31", text);
    }

    [Fact]
    public void Format_InvalidVinNeverPresentsBytesAsValidVin()
    {
        var bytes = new byte[19];
        bytes[0] = 0xF1;
        bytes[1] = 0x90;
        Array.Fill(bytes, (byte)'A', 2, 17);
        bytes[3] = (byte)'I'; // Forbidden VIN character.

        var text = Dvinholm.App.UdsDidDisplayFormatter.Format(0xF190, bytes);

        Assert.Contains("VIN format invalid", text);
        Assert.Contains("raw: F1 90", text);
        Assert.DoesNotContain("(VIN): ", text);
    }

    [Fact]
    public void Format_GenericPrintableDidShowsClearlyUnverifiedAsciiPreview()
    {
        var bytes = new byte[] { 0xF1, 0x87 }
            .Concat(Encoding.ASCII.GetBytes("ECM-123"))
            .ToArray();

        var text = Dvinholm.App.UdsDidDisplayFormatter.Format(0xF187, bytes);

        Assert.Contains("DID 0xF187", text);
        Assert.Contains("ASCII preview (unverified): \"ECM-123\"", text);
        Assert.Contains("raw: F1 87 45 43 4D 2D 31 32 33", text);
        Assert.DoesNotContain("(VIN)", text);
    }

    [Theory]
    [InlineData(new byte[] { 0x12, 0x34, 0x00, 0x10, 0x7F })]
    [InlineData(new byte[] { 0x12, 0x34, 0xFF, 0x80, 0x81 })]
    [InlineData(new byte[] { 0x12, 0x34, 0x41, 0x42 })]
    public void Format_BinaryOrShortPayloadDoesNotGuessAsciiOrUnits(byte[] response)
    {
        var text = Dvinholm.App.UdsDidDisplayFormatter.Format(0x1234, response);

        Assert.Contains("DID 0x1234", text);
        Assert.Contains("raw: 12 34", text);
        Assert.DoesNotContain("ASCII preview", text);
        Assert.DoesNotContain("°", text);
    }

    [Fact]
    public void Format_EmptyValueIsExplicitlyReported()
    {
        var text = Dvinholm.App.UdsDidDisplayFormatter.Format(
            0x1234, new byte[] { 0x12, 0x34 });

        Assert.Contains("no value bytes", text);
        Assert.Contains("raw: 12 34", text);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x12 })]
    [InlineData(new byte[] { 0x12, 0x35, 0x41 })]
    public void Format_RejectsTruncatedOrMismatchedDid(byte[] response)
    {
        Assert.Throws<InvalidOperationException>(() =>
            Dvinholm.App.UdsDidDisplayFormatter.Format(0x1234, response));
    }

    [Fact]
    public void Format_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Dvinholm.App.UdsDidDisplayFormatter.Format(0x1234, null!));
    }

    [Fact]
    public void Format_DoesNotClaimVeryLongBinaryValueIsText()
    {
        var response = new byte[] { 0x12, 0x34 }
            .Concat(Enumerable.Repeat((byte)'A', 129))
            .ToArray();

        var text = Dvinholm.App.UdsDidDisplayFormatter.Format(0x1234, response);

        Assert.Contains("raw: 12 34", text);
        Assert.DoesNotContain("ASCII preview", text);
    }
}
