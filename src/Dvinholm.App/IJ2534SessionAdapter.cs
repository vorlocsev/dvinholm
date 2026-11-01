namespace Dvinholm.App;

// The session needs adapter capabilities in addition to the transport contract.
// Keeping this as a narrow extension makes lifecycle tests independent of a vendor DLL.
internal interface IJ2534SessionAdapter : IJ2534Adapter
{
    J2534Native.AdapterCapabilities Capabilities { get; }

    bool ConnectConfigured(uint baudRate, bool extended, out string error)
    {
        if (!extended) return Connect(baudRate, out error);
        error = "This adapter does not implement explicit 29-bit channel selection.";
        return false;
    }
}
