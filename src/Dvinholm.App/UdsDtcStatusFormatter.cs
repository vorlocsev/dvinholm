namespace Dvinholm.App;

/// <summary>
/// Formats the ISO 14229 DTC status byte. These flags describe ECU-reported
/// status, not the root cause or severity of a diagnostic trouble code.
/// </summary>
internal static class UdsDtcStatusFormatter
{
    private static readonly (byte Mask, string Label)[] Flags =
    [
        (0x01, "test failed"),
        (0x02, "failed this operation cycle"),
        (0x04, "pending DTC"),
        (0x08, "confirmed DTC"),
        (0x10, "test not completed since last clear"),
        (0x20, "failed since last clear"),
        (0x40, "test not completed this operation cycle"),
        (0x80, "warning indicator requested")
    ];

    internal static string Format(byte status)
    {
        if (status == 0)
            return "no status bits set";

        var labels = Flags
            .Where(flag => (status & flag.Mask) != 0)
            .Select(flag => flag.Label);

        return string.Join(", ", labels);
    }
}
