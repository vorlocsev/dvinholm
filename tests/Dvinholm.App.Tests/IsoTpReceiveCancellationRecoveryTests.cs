using System.Collections.Concurrent;

namespace Dvinholm.App.Tests;

public sealed class IsoTpReceiveCancellationRecoveryTests
{
    [Fact]
    public void Request_CancellationWhileReceivingMultiFrameResponse_ReleasesChannelForNextRequest()
    {
        var adapter = new CancelDuringReceiveAdapter();
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        using var channel = new Dvinholm.App.IsoTpChannel(
            bus,
            new Dvinholm.App.IsoTpChannel.Options(
                0x7E0,
                0x7E8,
                FrameTimeout: TimeSpan.FromSeconds(2),
                ConsecutiveFrameTimeout: TimeSpan.FromSeconds(2),
                RequestTimeout: TimeSpan.FromSeconds(5)));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var error = Assert.ThrowsAny<OperationCanceledException>(
            () => channel.Request(new byte[] { 0x22, 0xF1, 0x90 }, cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(1, adapter.RequestCount);
        Assert.Equal(1, adapter.FlowControlCount);

        // The first response was left incomplete after its First Frame. A new
        // transaction must be able to acquire the channel and receive its own reply.
        var response = channel.Request(new byte[] { 0x22, 0xF1, 0x91 });

        Assert.Equal(new byte[] { 0x62, 0xF1, 0x91 }, response);
        Assert.Equal(2, adapter.RequestCount);
        Assert.Equal(1, adapter.FlowControlCount);
        Assert.NotNull(error);
    }

    private sealed class CancelDuringReceiveAdapter : Dvinholm.App.IJ2534Adapter
    {
        private readonly ConcurrentQueue<Dvinholm.App.J2534Native.PassthruMsg> _incoming = new();
        private readonly AutoResetEvent _incomingReady = new(false);
        private int _requestCount;
        private int _flowControlCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);
        internal int FlowControlCount => Volatile.Read(ref _flowControlCount);

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

        public bool Read(out Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (_incoming.TryDequeue(out msg))
                return true;

            _incomingReady.WaitOne(TimeSpan.FromMilliseconds(timeout));
            return _incoming.TryDequeue(out msg);
        }

        public bool Write(in Dvinholm.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            error = string.Empty;
            if (!Dvinholm.App.CanDecoder.TryDecode(msg, out var frame, out error))
                return false;

            var pciType = frame.Data[0] >> 4;
            if (pciType == 3)
            {
                Interlocked.Increment(ref _flowControlCount);
                // Deliberately withhold Consecutive Frames so the caller must
                // cancel while ISO-TP is waiting for the rest of the response.
            }
            else if (pciType == 0)
            {
                if (Interlocked.Increment(ref _requestCount) == 1)
                {
                    Enqueue(new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, 0x01, 0x02, 0x03 });
                }
                else
                {
                    Enqueue(new byte[] { 0x03, 0x62, 0xF1, 0x91 });
                }
            }

            return true;
        }

        private void Enqueue(byte[] data)
        {
            _incoming.Enqueue(Dvinholm.App.CanDecoder.Encode(0x7E8, data));
            _incomingReady.Set();
        }

        public void Unload() { }
    }
}
