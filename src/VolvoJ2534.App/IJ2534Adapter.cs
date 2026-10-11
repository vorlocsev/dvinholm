namespace VolvoJ2534.App;

internal interface IJ2534Adapter
{
    bool Load(string path, out string error);
    bool Open(out string error);
    bool Connect(uint baudRate, out string error);
    bool Read(out J2534Native.PassthruMsg msg, uint timeout, out string error);
    bool Write(in J2534Native.PassthruMsg msg, uint timeout, out string error);

    // Default implementations keep existing test/fake adapters source-compatible.
    // Real adapters can opt in when the vendor J2534 DLL supports message filters.
    bool StartCanIdFilter(uint arbitrationId, bool extended, out uint filterId, out string error)
    {
        filterId = 0;
        error = "Hardware CAN filters are not implemented by this adapter.";
        return false;
    }

    bool StopCanFilter(uint filterId, out string error)
    {
        error = "Hardware CAN filters are not implemented by this adapter.";
        return false;
    }

    void Unload();
}
