namespace Dvinholm.App.Tests;

/// <summary>
/// Eight end-to-end tests for the real read-only J2534 -> CAN dispatcher
/// -> ISO-TP -> UDS 0x22 -> snapshot path. All addresses/DIDs/bytes below
/// are SYNTHETIC fixtures, not usable Volvo P2/CEM specifications.
/// </summary>
public sealed class EcuSnapshotVirtualJ2534EndToEndTests
{
    [Fact]
    public void TwoDidCaptureUsesOnlySortedUdsReadsAndMonitorReceivesSameFrames()
    {
        var requests = new List<ushort>();
        var result = WithVirtualBus(
            request =>
            {
                if (TryReadDidRequest(request, out var did))
                {
                    requests.Add(did);
                    return did switch
                    {
                        0xF187 => [Frame(0x7E8, [0x05, 0x62, 0xF1, 0x87, 0xAA, 0x11])],
                        0xF189 => [Frame(0x7E8, [0x05, 0x62, 0xF1, 0x89, 0xBB, 0x22])],
                        _ => []
                    };
                }
                return [];
            },
            (client, adapter, bus) =>
            {
                using var monitor = bus.Subscribe();
                var snapshot = EcuConfigurationReadback.Capture(
                    Profile(0xF189, 0xF187), client);
                var received = new List<CanFrame>();
                for (var i = 0; i < 2; i++)
                {
                    Assert.True(monitor.TryRead(TimeSpan.FromSeconds(1),
                        CancellationToken.None, out var frame));
                    received.Add(frame);
                }
                return (snapshot, adapter.TransmittedFrames, received);
            });

        Assert.Equal(new ushort[] { 0xF187, 0xF189 }, requests);
        Assert.Equal(2, result.Item2);
        Assert.Equal(new[] { "AA11", "BB22" },
            result.snapshot.Values.Select(x => x.ValueHex));
        Assert.All(result.received, frame => Assert.Equal((uint)0x7E8, frame.ArbitrationId));
        Assert.False(result.snapshot.ProfileMarkedVerified);
    }

    [Fact]
    public void MultiFrameResponseCompletesOnlyAfterFlowControl()
    {
        var value = Enumerable.Range(1, 17).Select(n => (byte)n).ToArray();
        var udspayload = new byte[] { 0x62, 0xF1, 0x87 }.Concat(value).ToArray();
        var responseFrames = IsoTp.Segment(udspayload);
        Assert.True(responseFrames.Count > 1);
        var framesSent = new List<byte>();

        var result = WithVirtualBus(
            request =>
            {
                if (request.Data.Length == 0) return [];
                framesSent.Add(request.Data[0]);
                if (TryReadDidRequest(request, out var did) && did == 0xF187)
                    return [Frame(0x7E8, responseFrames[0])];
                if ((request.Data[0] >> 4) == 3)
                    return responseFrames.Skip(1).Select(frame => Frame(0x7E8, frame));
                return [];
            },
            (client, adapter, _) =>
                (EcuConfigurationReadback.Capture(Profile(0xF187), client),
                 adapter.TransmittedFrames));

        Assert.Equal(Convert.ToHexString(value), Assert.Single(result.Item1.Values).ValueHex);
        Assert.Equal(2, result.TransmittedFrames);
        Assert.Equal(0x03, framesSent[0]);
        Assert.Equal(3, framesSent[1] >> 4);
    }

    [Fact]
    public void SyntheticExtendedCanAddressUsesRealJ253429BitFraming()
    {
        const uint requestId = 0x18DA10F1;
        const uint responseId = 0x18DAF110;
        var seen = new List<CanFrame>();
        var snapshot = WithVirtualBus(
            request =>
            {
                seen.Add(request);
                return TryReadDidRequest(request, out var did) && did == 0xF187
                    ? [Frame(responseId, [0x04, 0x62, 0xF1, 0x87, 0xA5], extended: true)]
                    : [];
            },
            (client, _, _) => EcuConfigurationReadback.Capture(
                new EcuProfile("synthetic.extended", "Synthetic non-Volvo ECU",
                    requestId, responseId, true, [0xF187],
                    "Invented 29-bit fixture only", false), client),
            extended: true);

        Assert.Equal("A5", Assert.Single(snapshot.Values).ValueHex);
        Assert.Equal(requestId, snapshot.RequestId);
        Assert.Equal(responseId, snapshot.ResponseId);
        Assert.True(snapshot.IsExtended);
        Assert.Single(seen);
        Assert.True(seen[0].IsExtended);
    }

    [Fact]
    public void CapturedReadOnlyDataExportsAndInspectsWithoutGrantingSupport()
    {
        var snapshot = WithVirtualBus(
            request => TryReadDidRequest(request, out var did) && did == 0xF187
                ? [Frame(0x7E8, [0x05, 0x62, 0xF1, 0x87, 0xCA, 0xFE])]
                : [],
            (client, _, _) => EcuConfigurationReadback.Capture(
                Profile(0xF187), client));

        Assert.Throws<InvalidOperationException>(() =>
            EcuConfigurationSnapshotExport.CreateJson(snapshot, false));
        var json = EcuConfigurationSnapshotExport.CreateJson(
            snapshot, userApprovedSensitiveExport: true);
        var historical = EcuConfigurationSnapshotInspection.InspectJson(json);
        Assert.Equal("CAFE", Assert.Single(historical.Values).ValueHex);
        Assert.True(historical.ContainsPotentiallySensitiveData);
        Assert.False(historical.IsRestorableBackup);
        Assert.False(historical.ProfileMarkedVerified);
        Assert.Equal(VolvoP2CemSupportKind.Unknown,
            VolvoP2CemSupportMatrix.Assess(2007, snapshot,
                VolvoP2CemSupportMatrix.ProductionEntries).Kind);
    }

    [Fact]
    public void UdsNegativeReadDidResponseNeverCreatesSnapshot()
    {
        var sent = 0;
        WithVirtualBus(
            request =>
            {
                if (!TryReadDidRequest(request, out _)) return [];
                sent++;
                return [Frame(0x7E8, [0x03, 0x7F, 0x22, 0x31])];
            },
            (client, _, _) =>
            {
                var error = Assert.Throws<UdsNegativeResponseException>(() =>
                    EcuConfigurationReadback.Capture(Profile(0xF187), client));
                Assert.Equal((byte)0x22, error.RequestedService);
                Assert.Equal((byte)0x31, error.NegativeResponseCode);
                return 0;
            });
        Assert.Equal(1, sent);
    }

    [Fact]
    public void PreCancelledCaptureNeverTouchesTheVirtualAdapter()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var writes = WithVirtualBus(
            _ => throw new InvalidOperationException("CAN transmit was not expected"),
            (client, adapter, _) =>
            {
                Assert.ThrowsAny<OperationCanceledException>(() =>
                    EcuConfigurationReadback.Capture(Profile(0xF187),
                        client, cts.Token));
                return adapter.TransmittedFrames;
            });
        Assert.Equal(0, writes);
    }

    [Fact]
    public void CancellationAfterFirstRealUdsResponseAbortsNextDidRead()
    {
        using var cts = new CancellationTokenSource();
        var seen = new List<ushort>();
        var transmitted = WithVirtualBus(
            request =>
            {
                if (TryReadDidRequest(request, out var did) && did == 0xF187)
                {
                    seen.Add(did);
                    return [Frame(0x7E8, [0x04, 0x62, 0xF1, 0x87, 0xAA])];
                }
                throw new InvalidOperationException("A later DID read was attempted");
            },
            (client, adapter, _) =>
            {
                Assert.ThrowsAny<OperationCanceledException>(() =>
                    EcuConfigurationReadback.Capture(Profile(0xF189, 0xF187),
                        (did, ct) =>
                        {
                            var payload = client.ReadDataByIdentifier(did, ct);
                            cts.Cancel();
                            return payload;
                        }, cts.Token));
                return adapter.TransmittedFrames;
            });
        Assert.Equal(new ushort[] { 0xF187 }, seen);
        Assert.Equal(1, transmitted);
    }

    [Fact]
    public void SecondDidRejectionFailsWholeCaptureWithoutPartialBaseline()
    {
        var requested = new List<ushort>();
        WithVirtualBus(
            request =>
            {
                if (!TryReadDidRequest(request, out var did)) return [];
                requested.Add(did);
                return did switch
                {
                    0xF187 => [Frame(0x7E8, [0x04, 0x62, 0xF1, 0x87, 0xAA])],
                    0xF189 => [Frame(0x7E8, [0x03, 0x7F, 0x22, 0x31])],
                    _ => []
                };
            },
            (client, adapter, _) =>
            {
                Assert.Throws<UdsNegativeResponseException>(() =>
                    EcuConfigurationReadback.Capture(Profile(0xF189, 0xF187), client));
                Assert.Equal(2, adapter.TransmittedFrames);
                return 0;
            });
        Assert.Equal(new ushort[] { 0xF187, 0xF189 }, requested);
    }

    private static bool TryReadDidRequest(CanFrame request, out ushort did)
    {
        did = 0;
        if (request.Data.Length < 4 || request.Data[0] != 0x03 ||
            request.Data[1] != 0x22)
            return false;

        did = (ushort)((request.Data[2] << 8) | request.Data[3]);
        return true;
    }

    private static CanFrame Frame(uint id, byte[] bytes, bool extended = false) =>
        new(id, extended, false, bytes, 0, 0);

    private static EcuProfile Profile(params ushort[] dids) =>
        new("synthetic.virtual.cem", "Synthetic ECU (not a Volvo CEM)",
            0x7E0, 0x7E8, false, dids,
            "Virtual J2534 transport fixture only, no real ECU mapping", false);

    private static T WithVirtualBus<T>(
        Func<CanFrame, IEnumerable<CanFrame>> onTransmit,
        Func<UdsClient, VirtualJ2534Adapter, CanBus, T> action,
        bool extended = false)
    {
        using var adapter = new VirtualJ2534Adapter(onTransmit);
        using var bus = new CanBus(adapter);
        bus.Start();
        using var channel = new IsoTpChannel(bus, new IsoTpChannel.Options(
            RequestId: extended ? 0x18DA10F1u : 0x7E0u,
            ResponseId: extended ? 0x18DAF110u : 0x7E8u,
            CanExtendedId: extended,
            FrameTimeout: TimeSpan.FromSeconds(1),
            ConsecutiveFrameTimeout: TimeSpan.FromSeconds(1),
            RequestTimeout: TimeSpan.FromSeconds(3)));
        using var client = new UdsClient(channel);
        return action(client, adapter, bus);
    }
}
