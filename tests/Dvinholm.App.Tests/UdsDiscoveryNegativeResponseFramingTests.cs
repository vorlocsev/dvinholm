using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline guards against false passive ECU candidates from malformed
/// UDS negative responses. No physical J2534 adapter or vehicle is used.
/// </summary>
public sealed class UdsDiscoveryNegativeResponseFramingTests
{
    private static CanFrame Frame(uint id, params byte[] bytes) =>
        new(id, false, false, bytes, 0, 0);

    [Fact]
    public void ExactThreeByteNegativeResponseIsDetected()
    {
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x03, 0x7F, 0x22, 0x31), out var sid));
        Assert.Equal((byte)0x7F, sid);
    }

    [Fact]
    public void ExactThreeByteResponsePendingIsDetected()
    {
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x03, 0x7F, 0x19, 0x78), out var sid));
        Assert.Equal((byte)0x7F, sid);
    }

    [Fact]
    public void TruncatedNegativeResponseIsNotAnEcuCandidate()
    {
        Assert.False(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x02, 0x7F, 0x22), out var sid));
        Assert.Null(sid);
    }

    [Fact]
    public void FourByteNegativeResponseIsNotAnEcuCandidate()
    {
        Assert.False(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x04, 0x7F, 0x22, 0x78, 0x00), out var sid));
        Assert.Null(sid);
    }

    [Fact]
    public void LongerSingleFrameNegativeResponseIsNotAnEcuCandidate()
    {
        Assert.False(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x05, 0x7F, 0x19, 0x31, 0x00, 0x00), out var sid));
        Assert.Null(sid);
    }

    [Fact]
    public void FirstFrameBeginningWithNegativePrefixIsNotAnEcuCandidate()
    {
        // ISO-TP FF announces 8 payload bytes. A UDS negative response
        // cannot legitimately extend beyond its three-byte 7F SID NRC form.
        Assert.False(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x10, 0x08, 0x7F, 0x22, 0x78, 0x00, 0x00, 0x00), out var sid));
        Assert.Null(sid);
    }

    [Fact]
    public void ClassicCanPaddingOutsideThreeBytePayloadIsStillAllowed()
    {
        // SF PCI length 3, not CAN DLC 8, determines the UDS payload length.
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x03, 0x7F, 0x22, 0x31, 0xAA, 0xAA, 0xAA, 0xAA),
            out var sid));
        Assert.Equal((byte)0x7F, sid);
    }

    [Fact]
    public async Task PassiveScanIgnoresMalformedNegativesAndNeverTransmits()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        bus.Start();
        var scan = new UdsEcuDiscovery(bus).ScanAsync(TimeSpan.FromMilliseconds(500));
        await Task.Delay(75);

        adapter.InjectReceiveFrame(Frame(0x7E8, 0x03, 0x7F, 0x22, 0x31));
        adapter.InjectReceiveFrame(Frame(0x7E9, 0x03, 0x62, 0xF1, 0x90));
        adapter.InjectReceiveFrame(Frame(0x7EA, 0x04, 0x7F, 0x22, 0x78, 0x00));
        adapter.InjectReceiveFrame(Frame(0x7EB, 0x10, 0x08, 0x7F, 0x19, 0x78, 0, 0, 0));
        adapter.InjectReceiveFrame(Frame(0x7EC, 0x02, 0x7F, 0x22));

        var candidates = await scan.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.ResponseId == 0x7E8 && c.ResponseCount == 1);
        Assert.Contains(candidates, c => c.ResponseId == 0x7E9 && c.ResponseCount == 1);
        Assert.Equal(0, adapter.TransmittedFrames);
    }
}
