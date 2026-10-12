namespace Dvinholm.App.Tests;

public sealed class DisposeCancelsQueuedUdsRequestTests
{
    [Fact]
    public async Task Dispose_CancelsActiveRequestAndPreventsQueuedRequestFromBeingSent()
    {
        using var adapter = new VirtualJ2534Adapter(
            _ => Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromSeconds(5),
                RequestTimeout: TimeSpan.FromSeconds(5)));
        var client = new Dvinholm.App.UdsClient(channel);

        var activeRequest = Task.Run(
            () => Record.Exception(() => client.ReadDataByIdentifier(0xF190)));

        Assert.True(
            SpinWait.SpinUntil(() => adapter.TransmittedFrames == 1, TimeSpan.FromSeconds(1)),
            "The active UDS request did not reach the virtual adapter.");

        using var queuedStarted = new ManualResetEventSlim();
        var queuedRequest = Task.Run(() =>
        {
            queuedStarted.Set();
            return Record.Exception(() => client.ReadDataByIdentifier(0xF191));
        });

        Assert.True(queuedStarted.Wait(TimeSpan.FromSeconds(1)));
        await Task.Delay(50);

        client.Dispose();

        var activeError = await activeRequest.WaitAsync(TimeSpan.FromSeconds(1));
        var queuedError = await queuedRequest.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.IsAssignableFrom<OperationCanceledException>(activeError);
        Assert.NotNull(queuedError);
        Assert.True(
            queuedError is OperationCanceledException or ObjectDisposedException,
            $"Expected cancellation or disposed-client rejection, got {queuedError.GetType().Name}.");
        Assert.Equal(1, adapter.TransmittedFrames);

        channel.Dispose();
        client.Dispose();
    }
}
