namespace Dvinholm.App.Tests;

/// <summary>
/// Eight read-only UDS correlation regressions. A corrupt negative response
/// must not masquerade as an unrelated service and cause a timeout.
/// </summary>
public sealed class UdsMalformedUnrelatedNegativeTests
{
    private static readonly byte[] DidParameters = [0xF1, 0x91];

    [Fact]
    public void ValidNegativeForAnotherServiceRemainsUnrelated()
    {
        Assert.True(UdsClient.IsUnrelatedResponse(
            0x22, DidParameters, [0x7F, 0x19, 0x31]));
    }

    [Fact]
    public void ValidPendingForAnotherServiceRemainsUnrelated()
    {
        Assert.True(UdsClient.IsUnrelatedResponse(
            0x22, DidParameters, [0x7F, 0x19, 0x78]));
    }

    [Fact]
    public void TruncatedOtherServiceNegativeCannotBeIgnored()
    {
        Assert.False(UdsClient.IsUnrelatedResponse(
            0x22, DidParameters, [0x7F, 0x19]));
    }

    [Fact]
    public void PaddedOtherServiceNegativeCannotBeIgnored()
    {
        Assert.False(UdsClient.IsUnrelatedResponse(
            0x22, DidParameters, [0x7F, 0x19, 0x31, 0x00]));
    }

    [Fact]
    public void PaddedOtherServicePendingCannotBeIgnored()
    {
        Assert.False(UdsClient.IsUnrelatedResponse(
            0x22, DidParameters, [0x7F, 0x19, 0x78, 0x00]));
    }

    [Fact]
    public void IncompleteNegativePrefixIsNotTreatedAsUnrelated()
    {
        Assert.False(UdsClient.IsUnrelatedResponse(
            0x22, DidParameters, [0x7F]));
        Assert.False(UdsClient.IsUnrelatedResponse(
            0x22, DidParameters, []));
    }

    [Fact]
    public void CurrentServiceNegativeAndOtherPositiveKeepTheirCorrelation()
    {
        Assert.False(UdsClient.IsUnrelatedResponse(
            0x22, DidParameters, [0x7F, 0x22, 0x31]));
        Assert.True(UdsClient.IsUnrelatedResponse(
            0x22, DidParameters, [0x59, 0x02, 0xFF]));
        Assert.True(UdsClient.IsUnrelatedResponse(
            0x22, DidParameters, [0x62, 0xF1, 0x90, 0x42]));
    }

    [Fact]
    public void VirtualJ2534RejectsMalformedOtherServiceThenRecovers()
    {
        var requestCount = 0;
        using var adapter = new VirtualJ2534Adapter(frame =>
        {
            if (frame.Data.Length == 0 || (frame.Data[0] >> 4) != 0)
                return Array.Empty<CanFrame>();

            var request = Interlocked.Increment(ref requestCount);
            var response = request == 1
                ? new byte[] { 0x04, 0x7F, 0x19, 0x78, 0x00 }
                : new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 };
            return [new CanFrame(0x7E8, false, false, response, 0, 0)];
        });
        using var bus = new CanBus(adapter);
        bus.Start();
        using var channel = new IsoTpChannel(
            bus, new IsoTpChannel.Options(
                0x7E0, 0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(750),
                RequestTimeout: TimeSpan.FromSeconds(2)));
        using var uds = new UdsClient(channel);

        var error = Assert.Throws<InvalidOperationException>(
            () => uds.ReadDataByIdentifier(0xF191));
        Assert.Contains("exactly three bytes", error.Message);
        Assert.IsNotType<TimeoutException>(error);

        var valid = uds.ReadDataByIdentifier(0xF191);
        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42 }, valid);
        Assert.Equal(2, requestCount);
        Assert.Equal(2, adapter.TransmittedFrames);
    }
}
