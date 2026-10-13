namespace Dvinholm.App.Tests;

public sealed class CanIdParsingTests
{
    [Theory]
    [InlineData("7E0", 0x7E0u)]
    [InlineData("0x7FF", 0x7FFu)]
    [InlineData("000", 0u)]
    public void TryParseCanId_AcceptsValid11BitIds(string input, uint expected)
    {
        Assert.True(Dvinholm.App.MainWindow.TryParseCanId(
            input, extended: false, out var id, out var error), error);
        Assert.Equal(expected, id);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("800")]
    [InlineData("18DAF110")]
    [InlineData("")]
    [InlineData("not-hex")]
    public void TryParseCanId_RejectsInvalid11BitIds(string input)
    {
        Assert.False(Dvinholm.App.MainWindow.TryParseCanId(
            input, extended: false, out var id, out var error));
        Assert.Equal(0u, id);
        Assert.Contains("11-bit", error);
    }

    [Theory]
    [InlineData("0x0x7E0")]
    [InlineData("7E0x")]
    [InlineData("0x7E0x")]
    public void TryParseCanId_RejectsMisplacedOrRepeatedHexPrefix(string input)
    {
        Assert.False(Dvinholm.App.MainWindow.TryParseCanId(
            input, extended: false, out var id, out var error));
        Assert.Equal(0u, id);
        Assert.Contains("11-bit", error);
    }

    [Theory]
    [InlineData("18DAF110", 0x18DAF110u)]
    [InlineData("1FFFFFFF", 0x1FFFFFFFu)]
    [InlineData("0x00000000", 0u)]
    public void TryParseCanId_AcceptsValid29BitIds(string input, uint expected)
    {
        Assert.True(Dvinholm.App.MainWindow.TryParseCanId(
            input, extended: true, out var id, out var error), error);
        Assert.Equal(expected, id);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("20000000")]
    [InlineData("not-hex")]
    [InlineData("")]
    public void TryParseCanId_RejectsInvalid29BitIds(string input)
    {
        Assert.False(Dvinholm.App.MainWindow.TryParseCanId(
            input, extended: true, out var id, out var error));
        Assert.Equal(0u, id);
        Assert.Contains("29-bit", error);
    }
}
