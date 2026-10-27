namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic-only checks for read-only ECU configuration capture.
/// No J2534 driver, ECU, vehicle, PIN operation or UDS write is used.
/// </summary>
public sealed class EcuConfigurationReadbackTests
{
    [Fact]
    public void CaptureReadsSortedDidsAndPreservesExactHexValues()
    {
        var profile = Profile(0xF190, 0xF187);
        var requested = new List<ushort>();
        var result = EcuConfigurationReadback.Capture(profile, (did, _) =>
        {
            requested.Add(did);
            return did == 0xF187
                ? [0xF1, 0x87, 0x00, 0xAB, 0xCD]
                : [0xF1, 0x90, 0x59, 0x56, 0x31];
        });

        Assert.Equal(new ushort[] { 0xF187, 0xF190 }, requested);
        Assert.Equal(new ushort[] { 0xF187, 0xF190 }, result.Values.Select(x => x.DataIdentifier));
        Assert.Equal(new[] { "00ABCD", "595631" }, result.Values.Select(x => x.ValueHex));
    }

    [Fact]
    public void CaptureKeepsAddressingAndUnverifiedProvenance()
    {
        var profile = new EcuProfile("test.extended", "Synthetic ECU", 0x18DA10F1, 0x18DAF110,
            true, [0xF187], "Synthetic test only", isVerified: false);
        var result = EcuConfigurationReadback.Capture(profile, (_, _) => [0xF1, 0x87, 0x01]);

        Assert.Equal("test.extended", result.ProfileId);
        Assert.Equal("Synthetic ECU", result.ProfileName);
        Assert.Equal(0x18DA10F1u, result.RequestId);
        Assert.Equal(0x18DAF110u, result.ResponseId);
        Assert.True(result.IsExtended);
        Assert.False(result.ProfileMarkedVerified);
        Assert.InRange(result.CapturedAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Single(result.Values);
    }

    [Fact]
    public void EmptyProfileIsRejectedWithoutSendingRequests()
    {
        var requested = 0;
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationReadback.Capture(Profile(), (_, _) =>
            {
                requested++;
                return [0xF1, 0x90, 0x01];
            }));
        Assert.Equal(0, requested);
    }

    [Fact]
    public void MoreThan128DidsAreRejectedBeforeFirstRequest()
    {
        var dids = Enumerable.Range(0, 129).Select(x => (ushort)(0xF100 + x)).ToArray();
        var requests = 0;
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationReadback.Capture(Profile(dids), (_, _) =>
            {
                requests++;
                return [0xF1, 0x00, 0x01];
            }));
        Assert.Equal(0, requests);
    }

    [Fact]
    public void MismatchedDidEchoCannotBecomeConfigurationReadback()
    {
        var ex = Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187),
                (_, _) => [0xF1, 0x90, 0x00, 0x01]));
        Assert.Contains("0xF187", ex.Message);
    }

    [Fact]
    public void MissingOrEmptyDidValueIsRejected()
    {
        foreach (var payload in new byte[]?[]
        {
            null, [], [0xF1], [0xF1, 0x87]
        })
        {
            Assert.Throws<InvalidDataException>(() =>
                EcuConfigurationReadback.Capture(Profile(0xF187), (_, _) => payload!));
        }
    }

    [Fact]
    public void CancellationStopsBeforeOrBetweenDidReads()
    {
        using var before = new CancellationTokenSource();
        before.Cancel();
        var requests = 0;
        Assert.ThrowsAny<OperationCanceledException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187), (_, _) =>
            {
                requests++;
                return [0xF1, 0x87, 0x01];
            }, before.Token));
        Assert.Equal(0, requests);

        using var between = new CancellationTokenSource();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187, 0xF190), (did, _) =>
            {
                requests++;
                between.Cancel();
                return [0xF1, 0x87, 0x01];
            }, between.Token));
        Assert.Equal(1, requests);
    }

    [Fact]
    public void SecondDidFailureAbortsWithoutReturningPartialBaseline()
    {
        var calls = new List<ushort>();
        Assert.Throws<IOException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187, 0xF190), (did, _) =>
            {
                calls.Add(did);
                if (did == 0xF190)
                    throw new IOException("synthetic second DID failure");
                return [0xF1, 0x87, 0xAA];
            }));
        Assert.Equal(new ushort[] { 0xF187, 0xF190 }, calls);
    }

    private static EcuProfile Profile(params ushort[] dids) =>
        new("test.ecm", "Synthetic ECM", 0x7E0, 0x7E8, false, dids,
            "Synthetic test fixture; not verified against a vehicle.", false);
}
