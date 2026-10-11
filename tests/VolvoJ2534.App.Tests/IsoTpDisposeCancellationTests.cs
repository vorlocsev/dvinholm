namespace VolvoJ2534.App.Tests;

public sealed class IsoTpDisposeCancellationTests
{
    [Fact]
    public async Task Dispose_CancelsActiveRequestPromptly()
    {
        using var adapter = new VirtualJ2534Adapter(
            _ => Array.Empty<VolvoJ2534.App.CanFrame>());
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        bus.Start();

        var channel = new VolvoJ2534.App.IsoTpChannel(
            bus,
            new VolvoJ2534.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromSeconds(3),
                RequestTimeout: TimeSpan.FromSeconds(3)));

        var activeRequest = Task.Run(
            () => Record.Exception(() => channel.Request(new byte[] { 0x22, 0xF1, 0x90 })));

        Assert.True(
            SpinWait.SpinUntil(() => adapter.TransmittedFrames == 1, TimeSpan.FromSeconds(1)),
            "The request did not reach the virtual adapter.");

        channel.Dispose();

        var error = await activeRequest.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.Equal(1, adapter.TransmittedFrames);
    }
}
