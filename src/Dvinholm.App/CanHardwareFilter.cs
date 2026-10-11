namespace Dvinholm.App;

internal static unsafe class CanHardwareFilter
{
    internal static J2534Native.PassthruMsg CreateMask(bool extended)
    {
        var message = new J2534Native.PassthruMsg
        {
            ProtocolID = J2534Native.ProtocolCan,
            TxFlags = extended ? CanDecoder.Can29BitId : 0,
            DataSize = 4
        };

        var idMask = extended ? 0x1FFFFFFFu : 0x7FFu;
        message.Data[0] = (byte)(idMask >> 24);
        message.Data[1] = (byte)(idMask >> 16);
        message.Data[2] = (byte)(idMask >> 8);
        message.Data[3] = (byte)idMask;
        return message;
    }

    internal static J2534Native.PassthruMsg CreatePattern(uint arbitrationId, bool extended)
    {
        if ((!extended && arbitrationId > 0x7FF) || (extended && arbitrationId > 0x1FFFFFFF))
            throw new ArgumentOutOfRangeException(nameof(arbitrationId));

        var message = new J2534Native.PassthruMsg
        {
            ProtocolID = J2534Native.ProtocolCan,
            TxFlags = extended ? CanDecoder.Can29BitId : 0,
            DataSize = 4
        };

        message.Data[0] = (byte)(arbitrationId >> 24);
        message.Data[1] = (byte)(arbitrationId >> 16);
        message.Data[2] = (byte)(arbitrationId >> 8);
        message.Data[3] = (byte)arbitrationId;
        return message;
    }
}
