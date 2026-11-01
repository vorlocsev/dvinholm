namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline TX/RX separation tests. The in-memory test adapter is not a
/// real Volvo CEM or an authenticated J2534 hardware acceptance result.
/// </summary>
public sealed class IsoTpDistinctChannelIdTests
{
    [Fact]
    public void StandardSameCanIdIsRejectedBeforeAnyRequest()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new IsoTpChannel.Options(0x7E0, 0x7E0).Validate());
        Assert.Equal("ResponseId", error.ParamName);
        Assert.Contains("different", error.Message);
    }

    [Fact]
    public void ExtendedSameCanIdIsRejected()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new IsoTpChannel.Options(0x18DA10F1u, 0x18DA10F1u,
                CanExtendedId: true).Validate());
        Assert.Equal("ResponseId", error.ParamName);
    }

    [Fact]
    public void ZeroCanIdsAreNotAnExceptionToTheSeparationRule()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new IsoTpChannel.Options(0u, 0u).Validate());
        Assert.Equal("ResponseId", error.ParamName);
    }

    [Fact]
    public void StandardMaximumCanIdCannotEchoToItself()
    {
        Assert.Throws<ArgumentException>(() =>
            new IsoTpChannel.Options(0x7FFu, 0x7FFu).Validate());
    }

    [Fact]
    public void ExtendedMaximumCanIdCannotEchoToItself()
    {
        Assert.Throws<ArgumentException>(() =>
            new IsoTpChannel.Options(0x1FFFFFFFu, 0x1FFFFFFFu,
                CanExtendedId: true).Validate());
    }

    [Fact]
    public void DistinctStandardAndExtendedCanIdsRemainSupported()
    {
        foreach (var options in new[]
        {
            new IsoTpChannel.Options(0x7E0, 0x7E8),
            new IsoTpChannel.Options(0x7E8, 0x7E0),
            new IsoTpChannel.Options(0u, 0x7FFu),
            new IsoTpChannel.Options(0x18DA10F1, 0x18DAF110, CanExtendedId: true),
            new IsoTpChannel.Options(0x1FFFFFFFu, 0u, CanExtendedId: true)
        })
            options.Validate();
    }

    [Fact]
    public void RangeValidationStillRejectsInvalidCanIds()
    {
        var standard = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IsoTpChannel.Options(0x800u, 0x7E8u).Validate());
        Assert.Equal("RequestId", standard.ParamName);
        var extended = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IsoTpChannel.Options(0x18DA10F1u, 0x20000000u,
                CanExtendedId: true).Validate());
        Assert.Equal("ResponseId", extended.ParamName);
    }

    [Fact]
    public void DirectChannelConstructionRejectsSameIdsBeforeAdapterOrTransmit()
    {
        using var adapter = new VirtualJ2534Adapter(
            _ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        foreach (var options in new[]
        {
            new IsoTpChannel.Options(0x7E0, 0x7E0),
            new IsoTpChannel.Options(0x18DA10F1, 0x18DA10F1,
                CanExtendedId: true)
        })
        {
            var error = Assert.Throws<ArgumentException>(() =>
                new IsoTpChannel(bus, options));
            Assert.Equal("ResponseId", error.ParamName);
        }
        Assert.Equal(0, adapter.TransmittedFrames);

        // A valid channel can still be created on the same in-memory bus,
        // so a rejected construction does not poison the diagnostic layer.
        using var valid = new IsoTpChannel(
            bus, new IsoTpChannel.Options(0x7E0, 0x7E8));
        Assert.Equal(0, adapter.TransmittedFrames);
    }
}
