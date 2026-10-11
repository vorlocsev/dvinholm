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

        var activeRequest = Task.Run(
            () => Record.Exception(() => channel.Request(new byte[] { 0x22, 0xF1, 0x90 })));

        Assert.True(
            SpinWait.SpinUntil(() => adapter.TransmittedFrames == 1, TimeSpan.FromSeconds(1)),
            "The first request did not reach the virtual adapter.");

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var waitingError = Assert.ThrowsAny<OperationCanceledException>(
            () => channel.Request(new byte[] { 0x22, 0xF1, 0x91 }, cancellationToken: cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.NotNull(waitingError);

        var activeError = await activeRequest;
        Assert.IsType<TimeoutException>(activeError);
        Assert.Equal(1, adapter.TransmittedFrames);
    }
}
