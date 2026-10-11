namespace Dvinholm.App.Tests;

public sealed class IsoTpOptionsValidationTests
{
    [Theory]
    [InlineData(nameof(Dvinholm.App.IsoTpChannel.Options.FrameTimeout))]
    [InlineData(nameof(Dvinholm.App.IsoTpChannel.Options.FlowControlTimeout))]
    [InlineData(nameof(Dvinholm.App.IsoTpChannel.Options.ConsecutiveFrameTimeout))]
    [InlineData(nameof(Dvinholm.App.IsoTpChannel.Options.RequestTimeout))]
    public void Validate_RejectsNegativeTimeout(string timeoutName)
    {
        var invalidTimeout = TimeSpan.FromMilliseconds(-1);
        var options = timeoutName switch
        {
            nameof(Dvinholm.App.IsoTpChannel.Options.FrameTimeout) =>
                new Dvinholm.App.IsoTpChannel.Options(0x7E0, 0x7E8, FrameTimeout: invalidTimeout),
            nameof(Dvinholm.App.IsoTpChannel.Options.FlowControlTimeout) =>
                new Dvinholm.App.IsoTpChannel.Options(0x7E0, 0x7E8, FlowControlTimeout: invalidTimeout),
            nameof(Dvinholm.App.IsoTpChannel.Options.ConsecutiveFrameTimeout) =>
                new Dvinholm.App.IsoTpChannel.Options(0x7E0, 0x7E8, ConsecutiveFrameTimeout: invalidTimeout),
            nameof(Dvinholm.App.IsoTpChannel.Options.RequestTimeout) =>
                new Dvinholm.App.IsoTpChannel.Options(0x7E0, 0x7E8, RequestTimeout: invalidTimeout),
            _ => throw new ArgumentOutOfRangeException(nameof(timeoutName))
        };

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());

        Assert.Equal(timeoutName, error.ParamName);
    }

    [Fact]
    public void Validate_AcceptsZeroTimeoutsAsRequestsForDefaults()
    {
        var options = new Dvinholm.App.IsoTpChannel.Options(0x7E0, 0x7E8);

        options.Validate();

        Assert.Equal(TimeSpan.FromMilliseconds(1000), options.EffectiveFrameTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), options.EffectiveFlowControlTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), options.EffectiveConsecutiveFrameTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.EffectiveRequestTimeout);
    }

    [Fact]
    public void Validate_RejectsNegativeFlowControlWaitLimit()
    {
        var options = new Dvinholm.App.IsoTpChannel.Options(
            0x7E0,
            0x7E8,
            MaxWaitFlowControls: -1);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());

        Assert.Equal("MaxWaitFlowControls", error.ParamName);
    }
}
