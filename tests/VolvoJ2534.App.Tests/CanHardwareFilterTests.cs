namespace VolvoJ2534.App.Tests;

public sealed unsafe class CanHardwareFilterTests
{
    [Fact]
    public void CreateMask_UsesStandardCanIdentifierBits()
    {
        var mask = VolvoJ2534.App.CanHardwareFilter.CreateMask(extended: false);

        Assert.Equal(VolvoJ2534.App.J2534Native.ProtocolCan, mask.ProtocolID);
        Assert.Equal(4u, mask.DataSize);
        Assert.Equal(VolvoJ2534.App.CanDecoder.Can29BitId, mask.TxFlags);
        Assert.Equal(new byte[] { 0, 0, 7, 255 }, ReadId(mask));
    }

    [Fact]
    public void CreateMask_UsesExtendedCanIdentifierBits()
    {
        var mask = VolvoJ2534.App.CanHardwareFilter.CreateMask(extended: true);

        Assert.Equal(new byte[] { 31, 255, 255, 255 }, ReadId(mask));
    }

    [Theory]
    [InlineData(0x7E0u, false, 0u, 0u, 7u, 224u)]
    [InlineData(0x18DAF110u, true, 24u, 218u, 241u, 16u)]
    public void CreatePattern_EncodesCanIdAndFormat(
        uint id, bool extended, uint b0, uint b1, uint b2, uint b3)
    {
        var pattern = VolvoJ2534.App.CanHardwareFilter.CreatePattern(id, extended);

        Assert.Equal(VolvoJ2534.App.J2534Native.ProtocolCan, pattern.ProtocolID);
        Assert.Equal(4u, pattern.DataSize);
        Assert.Equal(extended ? VolvoJ2534.App.CanDecoder.Can29BitId : 0u, pattern.TxFlags);
        Assert.Equal(new byte[] { (byte)b0, (byte)b1, (byte)b2, (byte)b3 }, ReadId(pattern));
    }

    [Fact]
    public void CreatePattern_RejectsOutOfRangeIdentifiers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VolvoJ2534.App.CanHardwareFilter.CreatePattern(0x800, extended: false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VolvoJ2534.App.CanHardwareFilter.CreatePattern(0x20000000, extended: true));
    }

    private static byte[] ReadId(VolvoJ2534.App.J2534Native.PassthruMsg message)
    {
        var bytes = new byte[4];
        fixed (byte* source = message.Data)
        {
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = source[i];
        }
        return bytes;
    }
}
