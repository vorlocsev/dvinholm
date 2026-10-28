namespace Dvinholm.App.Tests;

/// <summary>Eight synthetic tests: no vehicle, J2534, PIN or ECU write.</summary>
public sealed class EcuConfigurationReadbackPayloadLimitTests
{
    [Fact]
    public void SingleByteOpaqueValueIsAccepted()
    {
        var snapshot = Capture(0xF187, 1);
        Assert.Single(snapshot.Values);
        Assert.Equal("AB", snapshot.Values[0].ValueHex);
    }

    [Fact]
    public void MaximumValueBytesFitClassicIsoTp()
    {
        var max = EcuConfigurationReadback.MaximumDidValueBytes;
        Assert.Equal(IsoTp.MaxPayloadLength - 3, max);
        var snapshot = Capture(0xF187, max);
        Assert.Equal(max * 2, snapshot.Values[0].ValueHex.Length);
        Assert.StartsWith("AB", snapshot.Values[0].ValueHex);
        Assert.False(snapshot.ProfileMarkedVerified);
    }

    [Fact]
    public void OneExcessByteFailsClosed()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Capture(0xF187,
            EcuConfigurationReadback.MaximumDidValueBytes + 1));
        Assert.Contains("0xF187", ex.Message);
        Assert.Contains("ISO-TP", ex.Message);
    }

    [Fact]
    public void ArbitrarilyHugeInjectedDidCannotBecomeSnapshot()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Capture(0xF187, 100_000));
        Assert.Contains("value limit", ex.Message);
    }

    [Fact]
    public void BadSecondDidDiscardsEntirePartialCapture()
    {
        var called = new List<ushort>();
        var ex = Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187, 0xF190), (did, _) =>
            {
                called.Add(did);
                return Response(did, did == 0xF187 ? 1
                    : EcuConfigurationReadback.MaximumDidValueBytes + 1);
            }));
        Assert.Equal(new ushort[] { 0xF187, 0xF190 }, called);
        Assert.Contains("0xF190", ex.Message);
    }

    [Fact]
    public void WrongDidEchoIsRejectedEvenAtMaximumLength()
    {
        var ex = Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187), (_, _) =>
                Response(0xF190, EcuConfigurationReadback.MaximumDidValueBytes)));
        Assert.Contains("echoed DID", ex.Message);
    }

    [Fact]
    public void CancellationAfterResponseAbortsSnapshot()
    {
        using var cts = new CancellationTokenSource();
        var calls = 0;
        Assert.ThrowsAny<OperationCanceledException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187), (did, _) =>
            {
                calls++;
                cts.Cancel();
                return Response(did, EcuConfigurationReadback.MaximumDidValueBytes);
            }, cts.Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void MaximumCaptureExportsOnlyWithConsentAsNonRestorableJson()
    {
        var snapshot = Capture(0xF187,
            EcuConfigurationReadback.MaximumDidValueBytes);
        Assert.Throws<InvalidOperationException>(() =>
            EcuConfigurationSnapshotExport.CreateJson(snapshot, false));
        var json = EcuConfigurationSnapshotExport.CreateJson(snapshot, true);
        var inspected = EcuConfigurationSnapshotInspection.InspectJson(json);
        Assert.Equal(snapshot.Values[0].ValueHex, inspected.Values[0].ValueHex);
        Assert.False(inspected.IsRestorableBackup);
        Assert.True(inspected.ContainsPotentiallySensitiveData);
        Assert.False(inspected.ProfileMarkedVerified);
    }

    private static EcuConfigurationReadSnapshot Capture(ushort did, int length) =>
        EcuConfigurationReadback.Capture(Profile(did), (requested, _) =>
            Response(requested, length));

    private static EcuProfile Profile(params ushort[] dids) =>
        new("synthetic.readback.boundary", "Synthetic ECU", 0x7E0, 0x7E8,
            false, dids, "Synthetic test only; not Volvo verified.", false);

    private static byte[] Response(ushort did, int length)
    {
        var bytes = new byte[checked(length + 2)];
        bytes[0] = (byte)(did >> 8);
        bytes[1] = (byte)did;
        Array.Fill(bytes, (byte)0xAB, 2, length);
        return bytes;
    }
}
