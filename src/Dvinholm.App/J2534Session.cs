namespace Dvinholm.App;

internal sealed class J2534Session : IDisposable
{
    private readonly J2534Native _j2534 = new();
    private readonly object _lifecycleGate = new();
    private CanBus? _bus;
    private bool _disposed;

    internal event Action<Exception>? ReadError;

    internal bool IsConnected => _bus is not null;

    internal J2534Native.AdapterVersionInfo? AdapterVersion { get; private set; }
    internal string AdapterVersionStatus { get; private set; } = string.Empty;
    internal J2534Native.AdapterCapabilities? AdapterCapabilities { get; private set; }

    internal CanBus Bus =>
        _bus ?? throw new InvalidOperationException("J2534 CAN session is not connected.");

    internal bool Connect(string dllPath, uint baudRate, out string error)
    {
        // Serialize all transitions around the native adapter. In particular,
        // a concurrent Disconnect must not unload the DLL while Connect is
        // still opening the device or starting the receive worker.
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return ConnectLocked(dllPath, baudRate, out error);
        }
    }

    private bool ConnectLocked(string dllPath, uint baudRate, out string error)
    {
        error = string.Empty;

        try
        {
            // A new connection attempt always replaces the previous session,
            // even when the new path is invalid.
            Disconnect();

            if (string.IsNullOrWhiteSpace(dllPath))
            {
                error = "J2534 DLL path is empty.";
                return false;
            }

            if (baudRate == 0)
            {
                error = "CAN baud rate must be greater than zero.";
                return false;
            }

            AdapterVersion = null;
            AdapterVersionStatus = string.Empty;
            AdapterCapabilities = null;
            if (!_j2534.Load(dllPath.Trim(), out error) ||
                !_j2534.Open(out error))
            {
                _j2534.Unload();
                return false;
            }

            AdapterCapabilities = _j2534.Capabilities;
            AdapterVersion = _j2534.ReadVersion(out var versionError);
            AdapterVersionStatus = AdapterVersion is null ? versionError : string.Empty;

            if (!_j2534.Connect(baudRate, out error))
            {
                _j2534.Unload();
                AdapterVersion = null;
                AdapterVersionStatus = string.Empty;
                AdapterCapabilities = null;
                return false;
            }

            _bus = new CanBus(_j2534);
            _bus.ReadError += HandleReadError;
            _bus.Start();
            return true;
        }
        catch (Exception ex)
        {
            // Vendor DLL entry points can throw despite their native ABI.
            // Cleanup must run for failures during Open/Connect as well as
            // failures while starting the receive worker.
            if (_bus is not null)
            {
                _bus.ReadError -= HandleReadError;
                try { _bus.Dispose(); }
                catch { /* Continue releasing the native session. */ }
                _bus = null;
            }

            _j2534.Unload();
            AdapterVersion = null;
            AdapterVersionStatus = string.Empty;
            AdapterCapabilities = null;
            error = ex.Message;
            return false;
        }
    }

    internal void Disconnect()
    {
        lock (_lifecycleGate)
        {
            if (_disposed)
                return;

            DisconnectLocked();
        }
    }

    private void DisconnectLocked()
    {
        if (_bus is not null)
        {
            _bus.ReadError -= HandleReadError;
            _bus.Dispose();
            _bus = null;
        }

        _j2534.Unload();
        AdapterVersion = null;
        AdapterVersionStatus = string.Empty;
        AdapterCapabilities = null;
    }

    private void HandleReadError(Exception error)
    {
        var handlers = ReadError;
        if (handlers is null)
            return;

        foreach (Action<Exception> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(error);
            }
            catch
            {
                // A UI/logging callback must not prevent other subscribers
                // from receiving the adapter diagnostic.
            }
        }
    }

    public void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (_disposed)
                return;

            // Mark the session disposed only after native teardown succeeds.
            // If a vendor driver throws during teardown, callers can retry
            // disposal rather than leaving a permanently half-disposed object.
            DisconnectLocked();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
