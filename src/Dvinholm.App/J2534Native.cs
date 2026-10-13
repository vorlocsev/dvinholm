using System.Runtime.InteropServices;

namespace Dvinholm.App;

internal sealed unsafe class J2534Native : IJ2534SessionAdapter, IDisposable
{
    private const uint StatusNoError = 0;
    private const uint ErrTimeout = 0x00000009;
    private const uint FilterPass = 0x00000001;
    internal const uint ProtocolCan = 5;
    internal const int MaxDataSize = 4128;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PassthruMsg
    {
        public uint ProtocolID;
        public uint RxStatus;
        public uint TxFlags;
        public uint Timestamp;
        public uint DataSize;
        public uint ExtraDataIndex;
        public fixed byte Data[MaxDataSize];
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruOpen(IntPtr name, out uint deviceId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruClose(uint deviceId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruConnect(uint deviceId, uint protocolId, uint flags, uint baudRate, out uint channelId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruDisconnect(uint channelId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruReadMsgs(uint channelId, PassthruMsg* msg, ref uint numMsgs, uint timeout);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruWriteMsgs(uint channelId, PassthruMsg* msg, ref uint numMsgs, uint timeout);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruGetLastError([Out] byte[] text);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruReadVersion(uint deviceId, [Out] byte[] firmwareVersion, [Out] byte[] dllVersion, [Out] byte[] apiVersion);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruStartMsgFilter(uint channelId, uint filterType, PassthruMsg* maskMsg, PassthruMsg* patternMsg, PassthruMsg* flowControlMsg, out uint filterId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruStopMsgFilter(uint channelId, uint filterId);

    private IntPtr _dll;
    private uint _device;
    private uint _channel;
    private PassThruOpen? _open;
    private PassThruClose? _close;
    private PassThruConnect? _connect;
    private PassThruDisconnect? _disconnect;
    private PassThruReadMsgs? _read;
    private PassThruWriteMsgs? _write;
    private PassThruGetLastError? _lastError;
    private PassThruReadVersion? _readVersion;
    internal sealed record AdapterVersionInfo(string Firmware, string Dll, string Api);
    internal sealed record AdapterCapabilities(bool HardwareCanFilters, bool VersionQuery, bool LastErrorDetails);
    internal AdapterVersionInfo? VersionInfo { get; private set; }
    internal AdapterCapabilities Capabilities => new(
        HardwareCanFilters: _startMsgFilter is not null && _stopMsgFilter is not null,
        VersionQuery: _readVersion is not null,
        LastErrorDetails: _lastError is not null);
    AdapterCapabilities IJ2534SessionAdapter.Capabilities => Capabilities;
    private PassThruStartMsgFilter? _startMsgFilter;
    private PassThruStopMsgFilter? _stopMsgFilter;
    private readonly HashSet<uint> _activeFilters = new();

    bool IJ2534Adapter.Load(string path, out string error) => Load(path, out error);
    bool IJ2534Adapter.Open(out string error) => Open(out error);
    bool IJ2534Adapter.Connect(uint baudRate, out string error) => Connect(baudRate, out error);
    bool IJ2534Adapter.Read(out PassthruMsg msg, uint timeout, out string error) => Read(out msg, timeout, out error);
    bool IJ2534Adapter.Write(in PassthruMsg msg, uint timeout, out string error) => Write(in msg, timeout, out error);
    bool IJ2534Adapter.StartCanIdFilter(uint arbitrationId, bool extended, out uint filterId, out string error) => StartCanIdFilter(arbitrationId, extended, out filterId, out error);
    bool IJ2534Adapter.StopCanFilter(uint filterId, out string error) => StopCanFilter(filterId, out error);
    AdapterVersionInfo? IJ2534Adapter.ReadVersion(out string error) => ReadVersion(out error);
    void IJ2534Adapter.Unload() => Unload();

    internal bool Load(string path, out string error)
    {
        error = string.Empty;
        try
        {
            // Re-loading this wrapper must not overwrite a live module handle.
            Unload();

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("J2534 DLL path is empty.");

            _dll = NativeLibrary.Load(path);
            _open = Get<PassThruOpen>("PassThruOpen");
            _close = Get<PassThruClose>("PassThruClose");
            _connect = Get<PassThruConnect>("PassThruConnect");
            _disconnect = Get<PassThruDisconnect>("PassThruDisconnect");
            _read = Get<PassThruReadMsgs>("PassThruReadMsgs");
            _write = Get<PassThruWriteMsgs>("PassThruWriteMsgs");
            _lastError = Get<PassThruGetLastError>("PassThruGetLastError");
            _readVersion = Get<PassThruReadVersion>("PassThruReadVersion");
            _startMsgFilter = Get<PassThruStartMsgFilter>("PassThruStartMsgFilter");
            _stopMsgFilter = Get<PassThruStopMsgFilter>("PassThruStopMsgFilter");

            if (_open is null || _close is null || _connect is null ||
                _disconnect is null || _read is null || _write is null)
                throw new InvalidOperationException("DLL does not expose the required J2534 functions.");

            return true;
        }
        catch (ArgumentException ex) when (string.IsNullOrWhiteSpace(path))
        {
            error = ex.Message;
            Unload();
            return false;
        }
        catch (Exception)
        {
            // NativeLibrary.Load exceptions often include the full selected
            // DLL path. Keep the UI actionable without echoing local paths.
            error = "Unable to load the selected J2534 DLL. Check that the file exists, required exports and dependencies are present, and the DLL matches the application's x86 architecture.";
            Unload();
            return false;
        }
    }

    private T? Get<T>(string name) where T : Delegate =>
        NativeLibrary.TryGetExport(_dll, name, out var address)
            ? Marshal.GetDelegateForFunctionPointer<T>(address)
            : null;

    internal bool Open(out string error)
    {
        error = string.Empty;
        if (_open is null) { error = "J2534 DLL is not loaded."; return false; }
        if (_device != 0)
        {
            error = "J2534 device is already open.";
            return false;
        }

        var result = _open(IntPtr.Zero, out _device);
        if (result != StatusNoError)
        {
            _device = 0;
            error = FormatFailure("PassThruOpen", result);
            return false;
        }
        if (_device == 0)
        {
            error = "PassThruOpen succeeded but returned an invalid device ID (0).";
            return false;
        }
        return true;
    }

    internal AdapterVersionInfo? ReadVersion(out string error)
    {
        error = string.Empty;
        VersionInfo = null;
        if (_device == 0)
        {
            error = "J2534 device is not open.";
            return null;
        }
        if (_readVersion is null)
        {
            error = "Driver does not export optional PassThruReadVersion.";
            return null;
        }

        // SAE J2534 specifies three null-terminated version strings. Use
        // fixed-size buffers and treat this diagnostic call as optional.
        var firmware = new byte[80];
        var dll = new byte[80];
        var api = new byte[80];
        try
        {
            var result = _readVersion(_device, firmware, dll, api);
            if (result != StatusNoError)
            {
                error = FormatFailure("PassThruReadVersion", result);
                return null;
            }

            VersionInfo = new AdapterVersionInfo(DecodeVersion(firmware), DecodeVersion(dll), DecodeVersion(api));
            return VersionInfo;
        }
        catch (Exception)
        {
            // Vendor interop exceptions can include machine-specific details.
            // Keep the operation identifiable without echoing raw exception text.
            error = "PassThruReadVersion failed due to an unexpected driver error.";
            return null;
        }
    }

    private static string DecodeVersion(byte[] buffer)
    {
        var length = Array.IndexOf(buffer, (byte)0);
        if (length < 0) length = buffer.Length;
        return System.Text.Encoding.ASCII.GetString(buffer, 0, length).Trim();
    }

    internal bool Connect(uint baudRate, out string error)
    {
        error = string.Empty;
        if (_connect is null || _device == 0)
        {
            error = "J2534 device is not open.";
            return false;
        }
        if (_channel != 0)
        {
            error = "J2534 CAN channel is already connected.";
            return false;
        }

        var result = _connect(_device, ProtocolCan, 0, baudRate, out _channel);
        if (result != StatusNoError)
        {
            _channel = 0;
            error = FormatFailure("PassThruConnect", result);
            return false;
        }
        if (_channel == 0)
        {
            error = "PassThruConnect succeeded but returned an invalid channel ID (0).";
            return false;
        }
        return true;
    }

    internal bool StartCanIdFilter(uint arbitrationId, bool extended, out uint filterId, out string error)
    {
        filterId = 0;
        error = string.Empty;
        if (_channel == 0 || _startMsgFilter is null || _stopMsgFilter is null)
        {
            error = "J2534 message filtering is unavailable or the CAN channel is not connected.";
            return false;
        }

        if ((!extended && arbitrationId > 0x7FF) || (extended && arbitrationId > 0x1FFFFFFF))
        {
            error = extended ? "29-bit CAN ID is out of range." : "11-bit CAN ID is out of range.";
            return false;
        }

        var mask = CanHardwareFilter.CreateMask(extended);
        var pattern = CanHardwareFilter.CreatePattern(arbitrationId, extended);
        var result = _startMsgFilter(_channel, FilterPass, &mask, &pattern, null, out filterId);
        if (result != StatusNoError)
        {
            filterId = 0;
            error = FormatFailure("PassThruStartMsgFilter", result);
            return false;
        }

        if (filterId == 0)
        {
            error = "PassThruStartMsgFilter succeeded but returned an invalid filter ID (0).";
            return false;
        }

        _activeFilters.Add(filterId);
        return true;
    }

    internal bool StopCanFilter(uint filterId, out string error)
    {
        error = string.Empty;
        if (!_activeFilters.Contains(filterId))
        {
            error = "J2534 filter is not active.";
            return false;
        }
        if (_channel == 0 || _stopMsgFilter is null)
        {
            error = "J2534 CAN channel is not connected.";
            return false;
        }

        var result = _stopMsgFilter(_channel, filterId);
        if (result != StatusNoError)
        {
            error = FormatFailure("PassThruStopMsgFilter", result);
            return false;
        }

        _activeFilters.Remove(filterId);
        return true;
    }

    internal bool Read(out PassthruMsg msg, uint timeout, out string error)
    {
        msg = default;
        error = string.Empty;
        if (_read is null || _channel == 0)
        {
            error = "J2534 CAN channel is not connected.";
            return false;
        }

        uint count = 1;
        fixed (PassthruMsg* pMsg = &msg)
        {
            var result = _read(_channel, pMsg, ref count, timeout);
            if (result == StatusNoError)
            {
                if (count == 1) return true;
                if (count == 0) return false; // No frame is not a driver error.
                error = "PassThruReadMsgs returned success with unexpected message count " + count + ".";
                return false;
            }

            if (result == ErrTimeout) return false;
            error = FormatFailure("PassThruReadMsgs", result);
            return false;
        }
    }

    internal bool Write(in PassthruMsg msg, uint timeout, out string error)
    {
        error = string.Empty;
        if (_write is null || _channel == 0)
        {
            error = "J2534 CAN channel is not connected.";
            return false;
        }

        var copy = msg;
        uint count = 1;
        var result = _write(_channel, &copy, ref count, timeout);
        if (result == StatusNoError && count == 1) return true;
        if (result == StatusNoError)
        {
            error = "PassThruWriteMsgs returned success with unexpected message count " + count + ".";
            return false;
        }

        error = FormatFailure("PassThruWriteMsgs", result);
        return false;
    }

    internal void Disconnect()
    {
        if (_channel == 0)
        {
            _activeFilters.Clear();
            return;
        }

        foreach (var filterId in _activeFilters.ToArray())
        {
            try
            {
                if (_stopMsgFilter is not null)
                    _stopMsgFilter(_channel, filterId);
            }
            catch
            {
                // Continue cleanup; disconnecting the channel releases driver filters.
            }
        }
        _activeFilters.Clear();

        if (_disconnect is not null)
        {
            _disconnect(_channel);
            _channel = 0;
        }
    }

    internal void Close()
    {
        if (_close is not null && _device != 0)
        {
            _close(_device);
            _device = 0;
        }
    }

    internal void Unload()
    {
        // Attempt every cleanup stage independently. A faulty vendor driver
        // must not prevent Close or leave delegates pointing at an unloaded DLL.
        try { Disconnect(); }
        catch { _channel = 0; }

        try { Close(); }
        catch { _device = 0; }

        _open = null;
        _close = null;
        _connect = null;
        _disconnect = null;
        _read = null;
        _write = null;
        _lastError = null;
        _readVersion = null;
        VersionInfo = null;
        _startMsgFilter = null;
        _stopMsgFilter = null;
        _activeFilters.Clear();

        var dll = _dll;
        _dll = IntPtr.Zero;
        if (dll != IntPtr.Zero)
        {
            try { NativeLibrary.Free(dll); }
            catch
            {
                // Cleanup is best-effort; never retain stale function pointers.
            }
        }
    }

    private string FormatFailure(string operation, uint status)
    {
        var detail = LastError();
        return string.IsNullOrWhiteSpace(detail)
            ? operation + " failed with J2534 status 0x" + status.ToString("X8") + "."
            : operation + " failed (J2534 status 0x" + status.ToString("X8") + "): " + detail;
    }

    private string LastError()
    {
        if (_lastError is null) return string.Empty;

        try
        {
            var buffer = new byte[256];
            var result = _lastError(buffer);
            if (result != StatusNoError) return string.Empty;
            var length = Array.IndexOf(buffer, (byte)0);
            if (length < 0) length = buffer.Length;
            return System.Text.Encoding.ASCII.GetString(buffer, 0, length).Trim();
        }
        catch
        {
            // A broken optional diagnostic export must not hide the original
            // J2534 status code or prevent session cleanup.
            return string.Empty;
        }
    }

    public void Dispose()
    {
        Unload();
        GC.SuppressFinalize(this);
    }
}