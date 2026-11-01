using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight read-only regressions for passive recognition of 0x62 UDS replies.
/// A ReadDataByIdentifier positive response requires two complete DID bytes.
/// </summary>
public sealed class UdsDiscoveryReadDidLengthTests
{
    private static CanFrame Frame(uint id, params byte[] data) =>
        new(id, false, false, data, 0, 0);

    [Fact]
    public void ServiceOnlyReadDidPositiveIsNotAnEcuCandidate()
    {
        Assert.False(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x01, 0x62), out var sid));
        Assert.Null(sid);
    }

    [Fact]
    public void ReadDidPositiveWithOnlyOneDidByteIsNotAnEcuCandidate()
    {
        Assert.False(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x02, 0x62, 0xF1), out var sid));
        Assert.Null(sid);
    }

    [Fact]
    public void FullTwoByteDidWithoutValueCanBeObserved()
    {
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x03, 0x62, 0xF1, 0x90), out var sid));
        Assert.Equal((byte)0x62, sid);
    }

    [Fact]
    public void ReadDidPositiveWithValueRemainsAccepted()
    {
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x05, 0x62, 0xF1, 0x91, 0x41, 0x42), out var sid));
        Assert.Equal((byte)0x62, sid);
    }

    [Fact]
    public void ClassicCanPaddingDoesNotHideShortDeclaredDid()
    {
        // The ISO-TP declared length governs DID completeness, not CAN DLC.
        Assert.False(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x02, 0x62, 0xF1, 0x90, 0, 0, 0, 0), out var invalidSid));
        Assert.Null(invalidSid);

        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x03, 0x62, 0xF1, 0x90, 0, 0, 0, 0), out var validSid));
        Assert.Equal((byte)0x62, validSid);
    }

    [Fact]
    public void ValidFirstFrameReadDidPositiveStillIdentifiesCandidate()
    {
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x10, 0x08, 0x62, 0xF1, 0x90, 0x41, 0x42, 0x43), out var sid));
        Assert.Equal((byte)0x62, sid);
    }

    [Fact]
    public void OtherPositiveAndWellFormedNegativeResponsesAreNotChanged()
    {
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x02, 0x50, 0x03), out var positiveSid));
        Assert.Equal((byte)0x50, positiveSid);
        Assert.True(UdsEcuDiscovery.IsLikelyUdsResponse(
            Frame(0x7E8, 0x03, 0x7F, 0x22, 0x31), out var negativeSid));
        Assert.Equal((byte)0x7F, negativeSid);
    }

    [Fact]
    public async Task VirtualPassiveScanRejectsIncompleteDidsWithoutSendingCan()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        bus.Start();

        var scan = new UdsEcuDiscovery(bus).ScanAsync(TimeSpan.FromSeconds(2));
        // The reader and scanner block on separate pool workers. On a two-core
        // host a delayed test continuation can miss the entire scan window.
        // Model periodic received traffic from a dedicated fixture worker.
        using var stopFeed = new CancellationTokenSource();
        var feed = Task.Factory.StartNew(() =>
        {
            do
            {
                adapter.InjectReceiveFrame(Frame(0x7E8, 0x02, 0x62, 0xF1));
                adapter.InjectReceiveFrame(Frame(0x7E9, 0x03, 0x62, 0xF1, 0x90));
                adapter.InjectReceiveFrame(Frame(0x7EA, 0x03, 0x7F, 0x22, 0x31));
                adapter.InjectReceiveFrame(Frame(0x7EB, 0x01, 0x62));
            } while (!stopFeed.Token.WaitHandle.WaitOne(10));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        IReadOnlyList<UdsEcuCandidate> candidates;
        try { candidates = await scan.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally
        {
            stopFeed.Cancel();
            await feed.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.ResponseId == 0x7E9 && c.ResponseCount >= 1);
        Assert.Contains(candidates, c => c.ResponseId == 0x7EA && c.ResponseCount >= 1);
        Assert.DoesNotContain(candidates, c => c.ResponseId is 0x7E8 or 0x7EB);
        Assert.Equal(0, adapter.TransmittedFrames);
    }
}
