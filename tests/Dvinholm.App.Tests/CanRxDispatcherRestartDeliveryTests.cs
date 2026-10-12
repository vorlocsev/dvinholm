using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class CanRxDispatcherRestartDeliveryTests
{
    [Fact]
    public async Task RestartResumesFrameDeliveryToExistingSubscription()
    {
        var adapter = new QueueJ2534Adapter();
        using var dispatcher = new Dvinholm.App.CanRxDispatcher(adapter);
        using var subscription = dispatcher.Subscribe();

        dispatcher.Start();
        adapter.Enqueue(Dvinholm.App.CanDecoder.Encode(0x7E8, new byte[] { 0x01, 0x00 }));

        Assert.True(subscription.TryRead(TimeSpan.FromSeconds(2), CancellationToken.None, out var first),
            "The initial worker did not deliver the first frame.");
        Assert.Equal((uint)0x7E8, first.ArbitrationId);

        dispatcher.Stop();

        // Enqueue only after Stop has returned, so the stopped worker cannot
        // consume this frame. The same subscription should work after restart.
        adapter.Enqueue(Dvinholm.App.CanDecoder.Encode(0x7E8, new byte[] { 0x02, 0x00 }));
        dispatcher.Start();

        try
        {
            Assert.True(subscription.TryRead(TimeSpan.FromSeconds(2), CancellationToken.None, out var second),
                "The restarted worker did not resume delivery to the existing subscription.");
            Assert.Equal((uint)0x7E8, second.ArbitrationId);
            Assert.Equal(new byte[] { 0x02, 0x00 }, second.Data);
        }
        finally
        {
            dispatcher.Stop();
        }
    }

    private sealed class QueueJ2534Adapter : Dvinholm.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<Dvinholm.App.J2534Native.PassthruMsg> _frames = new();

        internal void Enqueue(Dvinholm.App.J2534Native.PassthruMsg frame) => _frames.Enqueue(frame);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }
        public bool Write(in Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            return true;
        }
        public void Unload() { }

        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            if (_frames.TryDequeue(out msg))
            {
                error = string.Empty;
                return true;
            }

            Thread.Sleep((int)Math.Min(timeout, 10));
            msg = default;
            error = string.Empty;
            return false;
        }
    }
}
