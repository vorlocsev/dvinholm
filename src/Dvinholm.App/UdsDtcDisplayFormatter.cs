namespace Dvinholm.App;

/// <summary>
/// Displays the records reported by one read-only UDS ReadDTCInformation
/// (0x19/0x02) response. This is not a Volvo-specific code dictionary.
/// </summary>
internal static class UdsDtcDisplayFormatter
{
    /// <summary>
    /// Preserve the ECU-reported availability mask. Unsupported status bits
    /// remain visible in raw hexadecimal but must never be described as
    /// supported diagnostic flags.
    /// </summary>
    internal static string Format(UdsDtcReadResult read, byte requestedStatusMask = 0xFF)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(read.Records);

        var header = $"{read.Records.Count} DTC record(s) returned " +
            $"(requested status mask 0x{requestedStatusMask:X2}; " +
            $"ECU-supported status bits 0x{read.StatusAvailabilityMask:X2}).";
        if (read.Records.Count == 0)
            return header + " No records from this ECU; this does not confirm a fault-free vehicle.";

        var lines = new List<string>(read.Records.Count + 1) { header };
        foreach (var record in read.Records)
        {
            var supportedStatus = (byte)(record.Status & read.StatusAvailabilityMask);
            var unsupportedStatus = (byte)(record.Status & ~read.StatusAvailabilityMask);
            var formatted = UdsDtcStatusFormatter.Format(supportedStatus);
            var discrepancy = unsupportedStatus == 0
                ? string.Empty
                : $" [unsupported status bits 0x{unsupportedStatus:X2} " +
                  "reported outside ECU availability mask]";
            lines.Add($"DTC 0x{record.Code:X6} | raw status 0x{record.Status:X2}: " +
                formatted + discrepancy);
        }

        return string.Join(Environment.NewLine, lines);
    }

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
