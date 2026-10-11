namespace VolvoJ2534.App.Tests;

public sealed class IsoTpSeparationTimeValidationTests
{
    [Theory]
    [InlineData(0x00)]
    [InlineData(0x7F)]
    [InlineData(0xF1)]
    [InlineData(0xF9)]
    public void ValidateSeparationTime_AcceptsDefinedValues(int value)
    {
        VolvoJ2534.App.IsoTpChannel.ValidateSeparationTime((byte)value);
    }

    [Theory]
    [InlineData(0x80)]
    [InlineData(0xF0)]
    [InlineData(0xFA)]
    [InlineData(0xFF)]
    public void ValidateSeparationTime_RejectsReservedValues(int value)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => VolvoJ2534.App.IsoTpChannel.ValidateSeparationTime((byte)value));

        Assert.Contains($"0x{value:X2}", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
