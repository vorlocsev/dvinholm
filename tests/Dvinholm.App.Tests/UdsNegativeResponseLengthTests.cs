namespace Dvinholm.App.Tests;

/// <summary>
/// Eight read-only UDS negative-response framing regressions, including
/// a virtual J2534 round trip without real vehicle hardware.
/// </summary>
public sealed class UdsNegativeResponseLengthTests
{
    [Fact]
    public void ThreeByteResponsePendingIsStillInterim()
    {
        Assert.True(UdsClient.IsResponsePending(0x22, [0x7F, 0x22, 0x78]));
        Assert.True(UdsClient.IsResponsePending(0x19, [0x7F, 0x19, 0x78]));
    }

    [Fact]
    public void ExtraByteCannotTurnMalformedNegativeResponseIntoPending()
    {
        Assert.False(UdsClient.IsResponsePending(0x22, [0x7F, 0x22, 0x78, 0x00]));
        Assert.False(UdsClient.IsResponsePending(0x19, [0x7F, 0x19, 0x78, 0x00]));
    }

    [Fact]
    public void ShortResponseNeverLooksLikePending()
    {
        Assert.False(UdsClient.IsResponsePending(0x22, [0x7F, 0x22]));
        Assert.False(UdsClient.IsResponsePending(0x22, [0x7F]));
        Assert.False(UdsClient.IsResponsePending(0x22, []));
    }

    [Fact]
    public void OtherServiceOrNrcDoesNotLookLikePending()
    {
        Assert.False(UdsClient.IsResponsePending(0x22, [0x7F, 0x19, 0x78]));
        Assert.False(UdsClient.IsResponsePending(0x22, [0x7F, 0x22, 0x31]));
        Assert.False(UdsClient.IsResponsePending(0x22, [0x62, 0xF1, 0x90]));
    }

    [Fact]
    public void ValidThreeByteNrcRetainsStructuredServiceAndCode()
    {
        var error = Assert.Throws<UdsNegativeResponseException>(() =>
            UdsClient.ParsePositiveResponse(0x22, [0x7F, 0x22, 0x31]));
        Assert.Equal((byte)0x22, error.RequestedService);
        Assert.Equal((byte)0x31, error.NegativeResponseCode);
    }

    [Fact]
    public void PaddedReadDidNegativeResponseFailsAsMalformed()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            UdsClient.ParsePositiveResponse(0x22, [0x7F, 0x22, 0x31, 0x00]));
        Assert.Contains("exactly three bytes", error.Message);
        Assert.IsNotType<UdsNegativeResponseException>(error);
    }

    [Fact]
    public void ExtraBytesInReadDtcNegativeResponseAreNeverIgnored()
    {
        foreach (var response in new byte[][]
                 {
                     [0x7F, 0x19, 0x78, 0x00],
                     [0x7F, 0x19, 0x22, 0x11, 0x22]
                 })
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                UdsClient.ParsePositiveResponse(0x19, response));
            Assert.Contains("exactly three bytes", error.Message);
        }
    }

    [Fact]
    public void MalformedPendingFrameFailsImmediatelyAndNextVirtualRequestSucceeds()
    {
        var requestCount = 0;
        using var adapter = new VirtualJ2534Adapter(frame =>
        {
            if (frame.Data.Length == 0 || (frame.Data[0] >> 4) != 0)
                return Array.Empty<CanFrame>();

            var request = Interlocked.Increment(ref requestCount);
            var bytes = request == 1
                ? new byte[] { 0x04, 0x7F, 0x22, 0x78, 0x00 } // SF: 4-byte invalid NRC
                : new byte[] { 0x04, 0x62, 0xF1, 0x91, 0x42 }; // SF: valid DID
            return [new CanFrame(0x7E8, false, false, bytes, 0, 0)];
        });
        using var bus = new CanBus(adapter);
        bus.Start();
        using var channel = new IsoTpChannel(
            bus, new IsoTpChannel.Options(
                0x7E0, 0x7E8, RequestTimeout: TimeSpan.FromSeconds(2)));
        using var uds = new UdsClient(channel);

        var malformed = Assert.Throws<InvalidOperationException>(() =>
            uds.ReadDataByIdentifier(0xF191));
        Assert.Contains("exactly three bytes", malformed.Message);

        var valid = uds.ReadDataByIdentifier(0xF191);
        Assert.Equal(new byte[] { 0xF1, 0x91, 0x42 }, valid);
        Assert.Equal(2, requestCount);
        Assert.Equal(2, adapter.TransmittedFrames);
    }
}
