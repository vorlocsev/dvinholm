namespace Dvinholm.App.Tests;

public sealed class UdsEcuDiscoveryScanTests
{
    [Fact]
    public async Task ScanAsync_CollectsOnlyLikelyUdsResponsesWithoutTransmitting()
    {
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();
        var discovery = new Dvinholm.App.UdsEcuDiscovery(bus);

        var scan = discovery.ScanAsync(TimeSpan.FromMilliseconds(400));
        // Allow the scan task to establish its subscription before injecting
        // frames into the virtual adapter's receive queue.
        await Task.Delay(50);

        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x90 }, 0, 0));
        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x90 }, 0, 0));
        // Request SID and continuation frame must not be reported as ECUs.
        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E0, false, false, new byte[] { 0x03, 0x22, 0xF1, 0x90 }, 0, 0));
        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E9, false, false, new byte[] { 0x21, 0x62, 0xF1, 0x90 }, 0, 0));

        var candidates = await scan.WaitAsync(TimeSpan.FromSeconds(2));

        var candidate = Assert.Single(candidates);
        Assert.Equal((uint)0x7E8, candidate.ResponseId);
        Assert.Equal((uint?)0x7E0, candidate.RequestId);
        Assert.False(candidate.IsExtended);
        Assert.Equal(2, candidate.ResponseCount);
        Assert.Equal(4, candidate.MaxDataLength);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Fact]
    public void IsLikelyUdsResponse_RejectsTruncatedNegativeResponse()
    {
        var truncated = new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x02, 0x7F, 0x22 }, 0, 0);

        Assert.False(Dvinholm.App.UdsEcuDiscovery.IsLikelyUdsResponse(truncated, out _));
    }

    [Fact]
    public void IsLikelyUdsResponse_AcceptsNegativeResponseWithNrc()
    {
        var negative = new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x03, 0x7F, 0x22, 0x31 }, 0, 0);

        Assert.True(Dvinholm.App.UdsEcuDiscovery.IsLikelyUdsResponse(negative, out var service));
        Assert.Equal((byte)0x7F, service);
    }

    [Fact]
    public void GuessRequestId_InfersOnlySupportedStandardAddressing()
    {
        Assert.Equal((uint?)0x7E0, Dvinholm.App.UdsEcuDiscovery.GuessRequestId(0x7E8, false));
        Assert.Equal((uint?)0x18DA01F1,
            Dvinholm.App.UdsEcuDiscovery.GuessRequestId(0x18DAF101, true));
        Assert.Null(Dvinholm.App.UdsEcuDiscovery.GuessRequestId(0x18DAF101, false));
        Assert.Null(Dvinholm.App.UdsEcuDiscovery.GuessRequestId(0x18DA1201, true));
    }

    [Fact]
    public async Task ScanAsync_UnrelatedTrafficDoesNotCrowdOutUdsResponses()
    {
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();
        var discovery = new Dvinholm.App.UdsEcuDiscovery(bus);

        var scan = discovery.ScanAsync(
            TimeSpan.FromSeconds(2),
            receiveQueueCapacity: 1);
        await Task.Delay(50);

        // High-rate unrelated frames must be filtered before the bounded
        // subscription queue so they cannot evict a valid UDS response.
        var unrelated = new Dvinholm.App.CanFrame(
            0x123, false, false, new byte[] { 0x01, 0x02, 0x03 }, 0, 0);
        for (var i = 0; i < 1_000; i++)
            adapter.InjectReceiveFrame(unrelated);
        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x90 }, 0, 0));

        var candidates = await scan.WaitAsync(TimeSpan.FromSeconds(4));

        var candidate = Assert.Single(candidates);
        Assert.Equal((uint)0x7E8, candidate.ResponseId);
        Assert.Equal(1, candidate.ResponseCount);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Fact]
    public async Task ScanAsync_ReportsIncompleteResultWhenReceiveQueueDropsFrames()
    {
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();
        var discovery = new Dvinholm.App.UdsEcuDiscovery(bus);

        // A deliberately tiny queue makes overflow reproducible under a burst.
        var scan = discovery.ScanAsync(
            TimeSpan.FromMilliseconds(500),
            receiveQueueCapacity: 1);
        await Task.Delay(50);

        var response = new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x90 }, 0, 0);
        // Ensure at least one candidate is observed before deliberately
        // overflowing the queue, so this test proves partial-result retention.
        adapter.InjectReceiveFrame(response);
        await Task.Delay(50);
        for (var i = 0; i < 20_000; i++)
            adapter.InjectReceiveFrame(response);

        var error = await Assert.ThrowsAsync<Dvinholm.App.UdsEcuDiscoveryIncompleteException>(
            async () => await scan.WaitAsync(TimeSpan.FromSeconds(3)));

        Assert.Contains("scan is incomplete", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dropped", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(error.DroppedFrameCount > 0);
        Assert.Contains(error.Candidates, candidate => candidate.ResponseId == 0x7E8);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Fact]
    public async Task ScanAsync_PreservesObservedCandidatesWhenCancelled()
    {
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();
        var discovery = new Dvinholm.App.UdsEcuDiscovery(bus);
        using var cancellation = new CancellationTokenSource();

        var scan = discovery.ScanAsync(TimeSpan.FromSeconds(5), cancellation.Token);
        await Task.Delay(50);
        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x90 }, 0, 0));
        // Ensure the receive worker has time to dispatch and the scan to observe
        // the response before cancellation is requested.
        await Task.Delay(100);
        cancellation.Cancel();

        var error = await Assert.ThrowsAsync<Dvinholm.App.UdsEcuDiscoveryCancelledException>(
            async () => await scan.WaitAsync(TimeSpan.FromSeconds(2)));

        var candidate = Assert.Single(error.Candidates);
        Assert.Equal((uint)0x7E8, candidate.ResponseId);
        Assert.Equal((uint?)0x7E0, candidate.RequestId);
        Assert.Contains("cancelled", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0L, error.DroppedFrameCount);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Fact]
    public async Task ScanAsync_CancelAfterQueueOverflow_RetainsLossWarningAndPartialCandidates()
    {
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();
        var discovery = new Dvinholm.App.UdsEcuDiscovery(bus);
        using var cancellation = new CancellationTokenSource();

        var scan = discovery.ScanAsync(
            TimeSpan.FromSeconds(5),
            cancellation.Token,
            receiveQueueCapacity: 1);
        await Task.Delay(50);

        var response = new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x90 }, 0, 0);
        // Establish a candidate before inducing queue overflow, so cancellation
        // must preserve observations as well as the frame-loss warning.
        adapter.InjectReceiveFrame(response);
        await Task.Delay(50);

        for (var i = 0; i < 20_000; i++)
            adapter.InjectReceiveFrame(response);

        // Allow the virtual receive worker to publish the burst before
        // cancellation, matching the tested overflow scenario above.
        await Task.Delay(200);
        cancellation.Cancel();

        var error = await Assert.ThrowsAsync<Dvinholm.App.UdsEcuDiscoveryCancelledException>(
            async () => await scan.WaitAsync(TimeSpan.FromSeconds(3)));

        Assert.True(error.DroppedFrameCount > 0);
        Assert.Contains(error.Candidates, candidate => candidate.ResponseId == 0x7E8);
        Assert.Contains("dropped", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("incomplete", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, adapter.TransmittedFrames);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(7L)]
    public void CancelledScan_PreservesQueueDropCountAndWarnsWhenFramesWereLost(long dropped)
    {
        var candidates = new[]
        {
            new Dvinholm.App.UdsEcuCandidate(0x7E8, 0x7E0, false, 1, 4)
        };

        var error = new Dvinholm.App.UdsEcuDiscoveryCancelledException(candidates, dropped);

        Assert.Same(candidates, error.Candidates);
        Assert.Equal(dropped, error.DroppedFrameCount);
        Assert.Contains("cancelled", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("incomplete", error.Message, StringComparison.OrdinalIgnoreCase);
        if (dropped > 0)
        {
            Assert.Contains(dropped.ToString(), error.Message);
            Assert.Contains("dropped", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.DoesNotContain("dropped", error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ScanAsync_TracksMultipleEcusIndependently()
    {
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();
        var discovery = new Dvinholm.App.UdsEcuDiscovery(bus);

        var scan = discovery.ScanAsync(TimeSpan.FromMilliseconds(400));
        // Let the passive scan subscribe before injecting both ECU responses.
        await Task.Delay(50);

        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E8, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x90 }, 0, 0));
        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E9, false, false, new byte[] { 0x03, 0x62, 0xF1, 0x91 }, 0, 0));
        adapter.InjectReceiveFrame(new Dvinholm.App.CanFrame(
            0x7E9, false, false, new byte[] { 0x03, 0x7F, 0x22, 0x31 }, 0, 0));

        var candidates = await scan.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, candidates.Count);
        var first = Assert.Single(candidates, x => x.ResponseId == 0x7E8);
        Assert.Equal((uint?)0x7E0, first.RequestId);
        Assert.Equal(1, first.ResponseCount);

        var second = Assert.Single(candidates, x => x.ResponseId == 0x7E9);
        Assert.Equal((uint?)0x7E1, second.RequestId);
        Assert.Equal(2, second.ResponseCount);
        Assert.Equal(4, second.MaxDataLength);
        Assert.Equal(0, adapter.TransmittedFrames);
    }
}
