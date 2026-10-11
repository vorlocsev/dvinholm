namespace Dvinholm.App.Tests;

public sealed class IsoTpReconnectLifecycleTests
{
    [Fact]
    public void ChannelBoundToDisposedBusCannotTransmitAfterReplacementBusStarts()
    {
        using var oldAdapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        var oldBus = new Dvinholm.App.CanBus(oldAdapter);
        oldBus.Start();
        var oldChannel = new Dvinholm.App.IsoTpChannel(
            oldBus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(100),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(100),
                RequestTimeout: TimeSpan.FromMilliseconds(300)));

        oldBus.Dispose();

        using var newAdapter = new VirtualJ2534Adapter(request =>
        {
            if (request.ArbitrationId == 0x7E0 &&
                request.Data.Length >= 3 &&
                request.Data[1] == 0x3E)
            {
                return new[]
                {
                    new Dvinholm.App.CanFrame(
                        0x7E8, false, false,
                        new byte[] { 0x02, 0x7E, 0x00 },
                        0, 0)
                };
            }

            return Array.Empty<Dvinholm.App.CanFrame>();
        });
        using var newBus = new Dvinholm.App.CanBus(newAdapter);
        newBus.Start();
        using var newChannel = new Dvinholm.App.IsoTpChannel(
            newBus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromMilliseconds(250),
                ConsecutiveFrameTimeout: TimeSpan.FromMilliseconds(250),
                RequestTimeout: TimeSpan.FromSeconds(1)));

        try
        {
            Assert.Throws<ObjectDisposedException>(() => oldChannel.Request(new byte[] { 0x3E, 0x00 }));
            Assert.Equal(0, oldAdapter.TransmittedFrames);

            var response = newChannel.Request(new byte[] { 0x3E, 0x00 });

            Assert.Equal(new byte[] { 0x7E, 0x00 }, response);
            Assert.Equal(1, newAdapter.TransmittedFrames);
        }
        finally
        {
            oldChannel.Dispose();
        }
    }
}
