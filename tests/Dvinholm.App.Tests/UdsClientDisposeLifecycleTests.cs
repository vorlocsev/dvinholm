namespace Dvinholm.App.Tests;

public sealed class UdsClientDisposeLifecycleTests
{
    [Fact]
    public async Task Dispose_CancelsActiveUdsRequestAndRejectsFutureRequests()
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
                FrameTimeout: TimeSpan.FromSeconds(3),
                RequestTimeout: TimeSpan.FromSeconds(3)));
        var client = new Dvinholm.App.UdsClient(channel);

        var activeRequest = Task.Run(
            () => Record.Exception(() => client.ReadDataByIdentifier(0xF190)));

        Assert.True(
            SpinWait.SpinUntil(() => adapter.TransmittedFrames == 1, TimeSpan.FromSeconds(1)),
            "The UDS request did not reach the virtual adapter.");

        client.Dispose();

        var error = await activeRequest.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.Throws<ObjectDisposedException>(() => client.ReadDataByIdentifier(0xF191));
        Assert.Equal(1, adapter.TransmittedFrames);
        client.Dispose();
    }
}
