namespace Dvinholm.App;

internal sealed class J2534Session : IDisposable
{
    private readonly IJ2534SessionAdapter _j2534;

    internal J2534Session(IJ2534SessionAdapter? adapter = null) =>
        _j2534 = adapter ?? new J2534Native();
    private readonly object _lifecycleGate = new();
    private CanBus? _bus;
    private bool _disposed;
    private int _transportFaulted;

    internal event Action<Exception>? ReadError;

    internal bool IsConnected => _bus is not null && Volatile.Read(ref _transportFaulted) == 0;
    internal uint? ConnectedBaudRate { get; private set; }
    internal bool? ConnectedExtended { get; private set; }

    internal J2534Native.AdapterVersionInfo? AdapterVersion { get; private set; }
    internal string AdapterVersionStatus { get; private set; } = string.Empty;
    internal J2534Native.AdapterCapabilities? AdapterCapabilities { get; private set; }

    internal CanBus Bus => IsConnected && _bus is { } bus
        ? bus : throw new InvalidOperationException("J2534 CAN session is not connected.");

    // Preserve native resources for orderly disconnect, but reject further
    // diagnostic work when receive-filter state could not be restored.
    internal void InvalidateTransport() => Interlocked.Exchange(ref _transportFaulted, 1);

    internal bool Connect(string dllPath, uint baudRate, out string error, bool extended = false)
    {
        // Connect's failure cleanup can unload the DLL. Never allow that path
        // to run from a callback raised by the active native Read call.
        if (_bus?.IsOnReadWorker == true)
            throw new InvalidOperationException(
                "Connect cannot be called from a J2534 read callback. Queue it on another thread.");

        // Serialize all transitions around the native adapter. In particular,
        // a concurrent Disconnect must not unload the DLL while Connect is
        // still opening the device or starting the receive worker.
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return ConnectLocked(dllPath, baudRate, extended, out error);
        }
    }

    private bool ConnectLocked(string dllPath, uint baudRate, bool extended, out string error)
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

            ClearAdapterMetadata();
            if (!_j2534.Load(dllPath.Trim(), out error) ||
                !_j2534.Open(out error))
            {
                var operationError = error;
                error = CombineErrors(operationError, TryUnloadAdapter());
                ClearAdapterMetadata();
                return false;
            }

            AdapterCapabilities = _j2534.Capabilities;
            AdapterVersion = _j2534.ReadVersion(out var versionError);
            AdapterVersionStatus = AdapterVersion is null ? versionError : string.Empty;

            if (!_j2534.ConnectConfigured(baudRate, extended, out error))
            {
                var operationError = error;
                error = CombineErrors(operationError, TryUnloadAdapter());
                ClearAdapterMetadata();
                return false;
            }

            _bus = new CanBus(_j2534);
            _bus.ReadError += HandleReadError;
            _bus.Start();
            ConnectedBaudRate = baudRate;
            ConnectedExtended = extended;
            return true;
        }
        catch (Exception)
        {
            // Vendor DLL entry points and teardown can throw despite the
            // adapter contract. Cleanup failures must not escape this method
            // or leave stale connection metadata behind.
            var cleanupErrors = new List<string>();
            if (_bus is not null)
            {
                _bus.ReadError -= HandleReadError;
                try { _bus.Dispose(); }
                catch (Exception)
                {
                    cleanupErrors.Add("CAN bus cleanup failed due to an unexpected adapter error.");
                }
                finally
                {
                    _bus = null;
                }
            }

            var unloadError = TryUnloadAdapter();
            if (!string.IsNullOrWhiteSpace(unloadError))
                cleanupErrors.Add(unloadError);

            ClearAdapterMetadata();
            error = CombineErrors("J2534 connection failed due to an unexpected adapter error.", string.Join("; ", cleanupErrors));
            return false;
        }
    }

    private string TryUnloadAdapter()
    {
        try
        {
            _j2534.Unload();
            return string.Empty;
        }
        catch (Exception)
        {
            return "J2534 adapter cleanup failed due to an unexpected driver error.";
        }
    }

    private void ClearAdapterMetadata()
    {
        Interlocked.Exchange(ref _transportFaulted, 0);
        ConnectedBaudRate = null;
        ConnectedExtended = null;
        AdapterVersion = null;
        AdapterVersionStatus = string.Empty;
        AdapterCapabilities = null;
    }

    private static string CombineErrors(string operationError, string cleanupError)
    {
        if (string.IsNullOrWhiteSpace(operationError))
            return cleanupError;
        if (string.IsNullOrWhiteSpace(cleanupError))
            return operationError;
        return operationError + " " + cleanupError;
    }

    internal void Disconnect()
    {
        // A callback runs on the receive worker. Queue teardown rather than
        // waiting for the lifecycle lock: another thread may already hold it
        // while waiting for that same worker to exit.
        var requestedBus = _bus;
        if (requestedBus?.IsOnReadWorker == true)
        {
            // Bind deferred teardown to the session that requested it. A
            // reconnect can complete before this task acquires the lifecycle
            // gate; it must not disconnect that newer session.
            _ = Task.Run(() => DisconnectDeferred(requestedBus));
            return;
        }

        lock (_lifecycleGate)
        {
            if (_disposed)
                return;

            DisconnectLocked();
        }
    }

    private void DisconnectDeferred(CanBus requestedBus)
    {
        lock (_lifecycleGate)
        {
            if (_disposed || !ReferenceEquals(_bus, requestedBus))
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

        try
        {
            _j2534.Unload();
        }
        catch
        {
            // Do not expose vendor exception text: it may contain local paths
            // or other machine-specific details. A later Disconnect or
            // Dispose may retry unloading the adapter.
            throw new InvalidOperationException(
                "J2534 adapter cleanup failed due to an unexpected driver error.");
        }
        finally
        {
            // The CAN bus has already been disposed and detached. Even if a
            // vendor Unload call fails, do not report stale adapter metadata
            // as though the session were still connected.
            ClearAdapterMetadata();
        }
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
        // Do not unload the vendor DLL from inside one of its own Read calls,
        // and do not block the worker on the lifecycle lock during shutdown.
        if (_bus?.IsOnReadWorker == true)
        {
            _ = Task.Run(Dispose);
            return;
        }

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
