namespace Dvinholm.App;

/// <summary>
/// Known, read-only UDS 0x19/0x02 status-mask choices in Diagnostics.
/// Values are single-bit ISO 14229 status flags or 0xFF for all statuses.
/// No manufacturer-specific meanings or session changes are implied.
/// </summary>
internal static class UdsDtcStatusMaskSelection
{
    internal const byte DefaultMask = 0xFF;

    // Parse only exact UI-owned tags. A missing, malformed or modified
    // selection must not silently broaden the vehicle request to 0xFF.
    internal static bool TryParseTag(string? tag, out byte statusMask)
    {
        switch (tag)
        {
            case "FF":
                statusMask = 0xFF;
                return true;
            case "01":
                statusMask = 0x01;
                return true;
            case "04":
                statusMask = 0x04;
                return true;
            case "08":
                statusMask = 0x08;
                return true;
            case "80":
                statusMask = 0x80;
                return true;
            default:
                statusMask = 0;
                return false;
        }
    }
}
