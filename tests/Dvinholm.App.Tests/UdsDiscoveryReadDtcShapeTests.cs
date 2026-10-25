using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight read-only checks for the positive ReadDTCInformation response shape
/// accepted by passive ECU discovery. No physical J2534 or ECU is used.
/// </summary>
public sealed class UdsDiscoveryReadDtcShapeTests
{
    private static CanFrame Frame(uint id, params byte[] data) =>
        new(id, false, false, data, 0, 0);

    [Fact]
    public void MissingDtcAvailabilityMaskIsNotAnEcuCandidate()
    {
        Assert.False(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x02, 0x59, 0x02), out var sid));
        Assert.Null(sid);
    }

    [Fact]
    public void CompleteEmptyDtcListIsValidEvidence()
    {
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x03, 0x59, 0x02, 0xFF), out var sid));
        Assert.Equal((byte)0x59, sid);
    }

    [Fact]
    public void CompleteFourByteDtcRecordIsAccepted()
    {
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x07, 0x59, 0x02, 0xFF, 0x12, 0x34, 0x56, 0x08),
            out var sid));
        Assert.Equal((byte)0x59, sid);
    }

    [Fact]
    public void PartialDtcRecordIsRejected()
    {
        Assert.False(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x06, 0x59, 0x02, 0xFF, 0x12, 0x34, 0x56),
            out var sid));
        Assert.Null(sid);
    }

    [Fact]
    public void ExtraNonRecordByteIsRejectedEvenWithCanPadding()
    {
        Assert.False(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x04, 0x59, 0x02, 0xFF, 0x00, 0, 0, 0),
            out var sid));
        Assert.Null(sid);

        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x03, 0x59, 0x02, 0xFF, 0, 0, 0, 0),
            out var validSid));
        Assert.Equal((byte)0x59, validSid);
    }

    [Fact]
    public void FirstFrameUsesAnnouncedTotalLengthNotPartialFrameBytes()
    {
        // 11 total UDS bytes = 3-byte header + two complete DTC records.
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x10, 0x0B, 0x59, 0x02, 0xFF, 0x12, 0x34, 0x56),
            out var validSid));
        Assert.Equal((byte)0x59, validSid);

        // 10 total UDS bytes cannot hold complete four-byte DTC records.
        Assert.False(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x10, 0x0A, 0x59, 0x02, 0xFF, 0x12, 0x34, 0x56),
            out var invalidSid));
        Assert.Null(invalidSid);
    }

    [Fact]
    public void OtherSubfunctionsAndUdsServicesRemainUnaffected()
    {
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x02, 0x59, 0x01), out var otherSid));
        Assert.Equal((byte)0x59, otherSid);

        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x03, 0x62, 0xF1, 0x90), out var didSid));
        Assert.Equal((byte)0x62, didSid);

        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x03, 0x7F, 0x19, 0x31), out var negativeSid));
        Assert.Equal((byte)0x7F, negativeSid);
    }

    [Fact]
    public async Task VirtualPassiveScanRejectsMalformedDtcShapesWithoutSendingCan()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        bus.Start();

        var scan = new UdsEcuDiscovery(bus).ScanAsync(TimeSpan.FromMilliseconds(500));
        await Task.Delay(75);

        adapter.InjectReceiveFrame(Frame(0x7E8, 0x02, 0x59, 0x02));
        adapter.InjectReceiveFrame(Frame(0x7E9, 0x03, 0x59, 0x02, 0xFF));
        adapter.InjectReceiveFrame(Frame(0x7EA, 0x04, 0x59, 0x02, 0xFF, 0x00));
        adapter.InjectReceiveFrame(Frame(0x7EB, 0x10, 0x0B, 0x59, 0x02, 0xFF, 0x12, 0x34, 0x56));

        var candidates = await scan.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.ResponseId == 0x7E9 && c.ResponseCount == 1);
        Assert.Contains(candidates, c => c.ResponseId == 0x7EB && c.ResponseCount == 1);
        Assert.DoesNotContain(candidates, c => c.ResponseId is 0x7E8 or 0x7EA);
        Assert.Equal(0, adapter.TransmittedFrames);
    }
}
