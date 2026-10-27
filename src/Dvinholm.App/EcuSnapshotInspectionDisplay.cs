using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Minimal, safe summary of a previously inspected local diagnostic snapshot.
/// Show identifiers and counts only. Never echo raw DID values, profile notes,
/// VIN, or vehicle data into a status line or preview dialog.
/// </summary>
internal static class EcuSnapshotInspectionDisplay
{
    internal const string SafetyNotice =
        "Historical diagnostic data only. SHA-256 detects accidental changes but " +
        "does NOT prove the file source or live ECU identity. " +
        "Not a restorable ECU configuration backup. Raw DID values are hidden.";

    internal static string Build(EcuConfigurationExportDocument snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != 1 || snapshot.IsRestorableBackup ||
            !snapshot.ContainsPotentiallySensitiveData ||
            snapshot.CapturedAtUtc == default ||
            snapshot.CapturedAtUtc.Offset != TimeSpan.Zero ||
            snapshot.Values is null ||
            snapshot.Values.Count is < 1 or > EcuConfigurationReadback.MaximumDataIdentifiers)
            throw new InvalidDataException("Only validated, non-restorable UTC diagnostic snapshots may be inspected.");

        var ids = snapshot.Values
            .Select(value => value.DataIdentifier.ToString("X4", CultureInfo.InvariantCulture))
            .ToArray();

        return "Local DID snapshot inspected successfully." + Environment.NewLine +
               SafetyNotice + Environment.NewLine + Environment.NewLine +
               "Captured UTC: " +
               snapshot.CapturedAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
               Environment.NewLine +
               "CAN request / response: " + snapshot.RequestCanId + " / " + snapshot.ResponseCanId +
               (snapshot.IsExtended ? " (29-bit)" : " (11-bit)") + Environment.NewLine +
               "DID count: " + ids.Length.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
               "DID identifiers (values hidden): " + string.Join(", ", ids);
    }
}
