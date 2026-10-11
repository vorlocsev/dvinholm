using System.Collections.Concurrent;

namespace VolvoJ2534.App.Tests;

/// <summary>
/// In-memory J2534 adapter used to exercise the real CAN/ISO-TP stack without
/// loading a vendor DLL or requiring a physical interface.
/// </summary>
internal sealed class VirtualJ2534Adapter : VolvoJ2534.App.IJ2534Adapter, IDisposable
{
    private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _rx = new();
    private readonly SemaphoreSlim _rxReady = new(0);
    private readonly Func<VolvoJ2534.App.CanFrame, IEnumerable<VolvoJ2534.App.CanFrame>> _onTransmit;
    private int _disposed;
    private int _transmittedFrames;
    private int _nextFilterId;
    private readonly ConcurrentDictionary<uint, (uint Id, bool Extended)> _filters = new();

    internal VirtualJ2534Adapter(
        Func<VolvoJ2534.App.CanFrame, IEnumerable<VolvoJ2534.App.CanFrame>> onTransmit)
        => _onTransmit = onTransmit ?? throw new ArgumentNullException(nameof(onTransmit));

    internal int TransmittedFrames => Volatile.Read(ref _transmittedFrames);
    internal int ActiveFilterCount => _filters.Count;

    internal void InjectReceiveFrame(VolvoJ2534.App.CanFrame frame)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _rx.Enqueue(VolvoJ2534.App.CanDecoder.Encode(
            frame.ArbitrationId, frame.Data, frame.IsExtended));
        _rxReady.Release();
    }

    public bool Load(string path, out string error) { error = string.Empty; return true; }
    public bool Open(out string error) { error = string.Empty; return true; }
    public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

    public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
    {
        error = string.Empty;
        msg = default;

        if (!_rx.TryDequeue(out msg))
        {
            if (Volatile.Read(ref _disposed) != 0 || !_rxReady.Wait((int)Math.Min(timeout, int.MaxValue)))
                return false;

            if (!_rx.TryDequeue(out msg))
                return false;
        }

        return true;
    }

    public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
    {
        error = string.Empty;
        // CanDecoder.TryDecode models receive-side J2534 messages and therefore
        // reads RxStatus. This fake is handling a transmit call, where the
        // CAN_29BIT_ID flag is carried in TxFlags; mirror it into RxStatus so
        // the same CAN payload decoder can validate and decode the frame.
        var receiveView = msg;
        receiveView.RxStatus = msg.TxFlags;
        if (!VolvoJ2534.App.CanDecoder.TryDecode(receiveView, out var frame, out error))
            return false;

        Interlocked.Increment(ref _transmittedFrames);
        foreach (var response in _onTransmit(frame))
            InjectReceiveFrame(response);

        return true;
    }

    public bool StartCanIdFilter(uint arbitrationId, bool extended, out uint filterId, out string error)
    {
        filterId = 0;
        error = string.Empty;
        if ((!extended && arbitrationId > 0x7FF) || (extended && arbitrationId > 0x1FFFFFFF))
        {
            error = "CAN ID is out of range.";
            return false;
        }

        filterId = (uint)Interlocked.Increment(ref _nextFilterId);
        _filters[filterId] = (arbitrationId, extended);
        return true;
    }

    public bool StopCanFilter(uint filterId, out string error)
    {
        if (_filters.TryRemove(filterId, out _))
        {
            error = string.Empty;
            return true;
        }

        error = "J2534 filter is not active.";
        return false;
    }

    public void Unload() { _filters.Clear(); }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _rxReady.Dispose();
    }
}
