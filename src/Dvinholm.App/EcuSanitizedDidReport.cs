using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Generates a deliberately lossy, user-requested inventory from an already
/// inspected local diagnostic JSON snapshot. No ECU reads, PIN, restore bytes,
/// payload hashes, timestamps, profile identifiers or CAN addresses are exported.
/// DID numbers and lengths can still fingerprint a vehicle, so the report
/// explicitly asks the user to review it before sharing.
/// </summary>
internal static class EcuSanitizedDidReport
{
    internal static string Build(EcuConfigurationExportDocument inspected)
    {
        ArgumentNullException.ThrowIfNull(inspected);
        if (inspected.SchemaVersion != 1 ||
            inspected.IsRestorableBackup ||
            !inspected.ContainsPotentiallySensitiveData ||
            inspected.CapturedAtUtc == default ||
            inspected.CapturedAtUtc.Offset != TimeSpan.Zero ||
            inspected.Values is null ||
            inspected.Values.Count is < 1 or > EcuConfigurationReadback.MaximumDataIdentifiers)
            throw new InvalidDataException("A validated non-restorable diagnostic snapshot is required.");

        var rows = new List<string>(inspected.Values.Count);
        ushort? lastDid = null;
        foreach (var value in inspected.Values)
        {
            if (value is null ||
                (lastDid is not null && value.DataIdentifier <= lastDid.Value) ||
                string.IsNullOrEmpty(value.ValueHex) ||
                value.ValueHex.Length > EcuConfigurationSnapshotExport.MaximumValueHexCharacters ||
                (value.ValueHex.Length & 1) != 0 ||
                value.ValueHex.Any(c => c is not (>= '0' and <= '9' or >= 'A' and <= 'F')))
                throw new InvalidDataException("Unvalidated, unsorted or duplicate DID entry.");
            lastDid = value.DataIdentifier;
            rows.Add(value.DataIdentifier.ToString("X4", CultureInfo.InvariantCulture) +
                " | " + (value.ValueHex.Length / 2).ToString(CultureInfo.InvariantCulture));
        }

        var output = new StringBuilder()
            .AppendLine("DVINHOLM REDACTED DID INVENTORY")
            .AppendLine("Offline research only. NOT a restorable ECU configuration backup.")
            .AppendLine("Not verified physical CEM identity, configuration mapping or PIN authorization.")
            .AppendLine("VIN, profile metadata, CAN addresses, UTC timestamps, payloads and payload hashes omitted.")
            .AppendLine("DID identifiers and lengths may still fingerprint a vehicle. Review before sharing.")
            .Append("DID count: ").AppendLine(rows.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine("DID | Bytes");
        foreach (var row in rows)
            output.AppendLine(row);
        return output.ToString();
    }

    internal static Task SaveAsync(
        EcuConfigurationExportDocument inspected,
        string destinationPath,
        string applicationDirectory)
    {
        // Validate before opening a destination, including blocked install folders.
        var report = Build(inspected);
        if (ReportExportDestinationGuard.IsInsideApplicationDirectory(
                destinationPath, applicationDirectory))
            throw new IOException(ReportExportDestinationGuard.BlockedMessage);
        return ReportExportFileWriter.WriteTextAsync(destinationPath, report);
    }
}
