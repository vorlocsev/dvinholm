namespace Dvinholm.App.Tests;

public sealed class IsoTpChannelWaitCancellationTests
{
    [Fact]
    public async Task Request_CancellationWhileWaitingForBusyChannel_DoesNotWaitForActiveRequest()
    {
        using var adapter = new VirtualJ2534Adapter(
            _ => Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(700),
                RequestTimeout: TimeSpan.FromMilliseconds(700)));

        var activeRequest = Task.Factory.StartNew(
            () => Record.Exception(() => channel.Request(new byte[] { 0x22, 0xF1, 0x90 })),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        Assert.True(
            SpinWait.SpinUntil(() => adapter.TransmittedFrames == 1, TimeSpan.FromSeconds(1)),
            "The first request did not reach the virtual adapter.");

        using var cancellation = new CancellationTokenSource();
        // Native-style blocking reads can exhaust the test pool. A dedicated
        // cancellation worker tests channel waiting rather than timer scheduling.
        var cancel = Task.Factory.StartNew(() =>
        {
            Thread.Sleep(100);
            cancellation.Cancel();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var waitingError = Assert.ThrowsAny<OperationCanceledException>(
            () => channel.Request(new byte[] { 0x22, 0xF1, 0x91 }, cancellationToken: cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.NotNull(waitingError);
        await cancel;

        var activeError = await activeRequest;
        Assert.IsType<TimeoutException>(activeError);
        Assert.Equal(1, adapter.TransmittedFrames);
    }
}
