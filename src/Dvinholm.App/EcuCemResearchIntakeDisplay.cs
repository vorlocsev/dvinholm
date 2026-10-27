using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// A human-shareable, explicitly opt-in research *intake*, built from a
/// previously inspected historical DID snapshot. Never exposes DID bytes,
/// VIN, owner details, profile fields, a file path or any credentials.
/// DOES NOT recognize a real Volvo CEM or permit a diagnostic command.
/// </summary>
internal static class EcuCemResearchIntakeDisplay
{
    internal const string BoundaryNotice =
        "OFFLINE RESEARCH METADATA ONLY. The ECU is NOT confirmed as an XC90 P2 CEM. " +
        "No model year, hardware/software revision, legitimate PIN route, option value " +
        "or write permission has been verified. A file checksum is not source authentication.";

    /// <summary>
    /// Only caller-validated, historical DID export data can be summarized.
    /// The result is metadata that a person can choose to copy for research.
    /// CAN IDs and DID lengths may still reveal a vehicle fingerprint:
    /// require an explicit user action before placing the text on clipboard.
    /// </summary>
    internal static string Build(EcuConfigurationExportDocument snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != 1 || snapshot.IsRestorableBackup ||
            !snapshot.ContainsPotentiallySensitiveData ||
            snapshot.CapturedAtUtc == default ||
            snapshot.CapturedAtUtc.Offset != TimeSpan.Zero ||
            snapshot.Values is null ||
            snapshot.Values.Count is < 1 or > EcuConfigurationReadback.MaximumDataIdentifiers)
            throw new InvalidDataException("A valid historical diagnostic snapshot is required.");

        int width = snapshot.IsExtended ? 8 : 3;
        if (!ValidCanId(snapshot.RequestCanId, width) ||
            !ValidCanId(snapshot.ResponseCanId, width) ||
            snapshot.RequestCanId == snapshot.ResponseCanId)
            throw new InvalidDataException("Invalid historical CAN identifiers.");

        var seen = new HashSet<ushort>();
        ushort? previous = null;
        foreach (var value in snapshot.Values)
        {
            if (value is null || !seen.Add(value.DataIdentifier) ||
                (previous is not null && value.DataIdentifier <= previous.Value) ||
                value.ValueHex is not { Length: >= 2 and <= EcuConfigurationSnapshotExport.MaximumValueHexCharacters } ||
                (value.ValueHex.Length & 1) != 0 ||
                !value.ValueHex.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
                throw new InvalidDataException("Invalid historical DID record ordering or byte length.");
            previous = value.DataIdentifier;
        }

        var output = new StringBuilder();
        output.AppendLine("Dvinholm | P2/CEM compatibility research intake");
        output.AppendLine(BoundaryNotice);
        output.AppendLine();
        output.Append("Historical capture (UTC): ").AppendLine(
            snapshot.CapturedAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture));
        output.Append("Historical transport: CAN ").Append(snapshot.IsExtended ? "29-bit" : "11-bit")
            .Append("; request ").Append(snapshot.RequestCanId)
            .Append("; response ").AppendLine(snapshot.ResponseCanId);
        output.Append("Read-only DID metadata: ").Append(snapshot.Values.Count)
            .AppendLine(" identifier(s), values omitted");
        foreach (var item in snapshot.Values)
            output.Append("  DID ").Append(item.DataIdentifier.ToString("X4", CultureInfo.InvariantCulture))
                .Append(": ").Append(item.ValueHex.Length / 2).AppendLine(" byte(s); value hidden");

        output.AppendLine();
        output.AppendLine("Still needed: independently confirmed XC90 model year, precise ECU/CEM identity,");
        output.AppendLine("sourced firmware revision/transport mapping and authorized bench evidence.");
        output.AppendLine("No VIN, profile ID/name, DID bytes, source file path or PIN is included.");
        output.AppendLine("This report is NOT a configuration backup and cannot authorize reads, writes or PIN acquisition.");
        output.AppendLine("Only share after reviewing CAN IDs and DID sizes for privacy.");
        return output.ToString();
    }

    private static bool ValidCanId(string? value, int width) =>
        value is not null && value.Length == width &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') &&
        uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id) &&
        id <= (width == 3 ? 0x7FFu : 0x1FFFFFFFu);
}
