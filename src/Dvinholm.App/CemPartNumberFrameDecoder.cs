namespace Dvinholm.App;

/// <summary>
/// Stateful, pure decoder of the two source-defined P2 CEM part-number reply
/// fragments. It never interacts with J2534, sends requests, or authenticates
/// a physical CEM. Each read operation owns its own decoder instance.
/// </summary>
internal sealed class CemPartNumberFrameDecoder
{
    private uint? _firstSixDigits;
    private uint _firstArbitrationId;

    internal void Reset()
    {
        _firstSixDigits = null;
        _firstArbitrationId = 0;
    }

    /// <summary>
    /// Returns an 7/8-digit part-number-shaped observation only for a complete
    /// pair from one extended, non-remote source ID. Invalid second fragments
    /// consume a pending first fragment instead of leaving reusable state.
    /// </summary>
    internal uint? Observe(CanFrame frame)
    {
        if (!frame.IsExtended || frame.IsRemote || frame.Data is not { Length: 8 } ||
            (frame.ArbitrationId & 0xFFFFu) != 3u)
            return null;

        var bytes = frame.Data;
        if ((bytes[0] & 0x80) != 0)
        {
            // Any new first fragment replaces an incomplete candidate.
            Reset();
            if (TryBcd(bytes[5], out var a) &&
                TryBcd(bytes[6], out var b) &&
                TryBcd(bytes[7], out var c))
            {
                _firstSixDigits = a * 10_000 + b * 100 + c;
                _firstArbitrationId = frame.ArbitrationId;
            }
            return null;
        }

        if (_firstSixDigits is not uint first)
            return null;

        var sourceId = _firstArbitrationId;
        Reset();
        if (sourceId != frame.ArbitrationId || (bytes[0] & 0x40) != 0 ||
            !TryBcd(bytes[1], out var last))
            return null;

        var partNumber = first * 100 + last;
        return partNumber is >= 1_000_000 and <= 99_999_999
            ? partNumber : null;
    }

    private static bool TryBcd(byte value, out uint decoded)
    {
        decoded = 0;
        if ((value & 15) > 9 || (value >> 4) > 9) return false;
        decoded = (uint)((value >> 4) * 10 + (value & 15));
        return true;
    }
}
