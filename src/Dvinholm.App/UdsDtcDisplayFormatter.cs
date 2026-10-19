namespace Dvinholm.App;

/// <summary>
/// Displays the records reported by one read-only UDS ReadDTCInformation
/// (0x19/0x02) response. This is not a Volvo-specific code dictionary.
/// </summary>
internal static class UdsDtcDisplayFormatter
{
    internal static string Format(IReadOnlyList<UdsDtcRecord> records, byte requestedStatusMask = 0xFF)
    {
        ArgumentNullException.ThrowIfNull(records);

        // A mask is scoped to one ECU's response, not the entire vehicle.
        var header = records.Count == 1
            ? $"1 DTC record returned (requested status mask 0x{requestedStatusMask:X2})"
            : $"{records.Count} DTC records returned (requested status mask 0x{requestedStatusMask:X2})";

        if (records.Count == 0)
            return header + ". This does not confirm that the vehicle is fault-free.";

        var lines = new List<string>(records.Count + 1) { header };
        foreach (var record in records)
        {
            // These are raw 24-bit UDS DTC identifiers. Without validated
            // vehicle-specific metadata, do not invent P-codes or fault causes.
            var status = UdsDtcStatusFormatter.Format(record.Status);
            lines.Add($"DTC 0x{record.Code:X6} | status 0x{record.Status:X2}: {status}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
