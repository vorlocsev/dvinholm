using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App;

/// <summary>
/// Human-readable diagnostic export only. An opaque DID readback is NOT a
/// recoverable ECU configuration backup. Raw DID values may contain a VIN
/// or other sensitive vehicle information, even if DID F190 is absent.
/// </summary>
internal sealed record EcuConfigurationExportDocument(
    int SchemaVersion,
    bool IsRestorableBackup,
    bool ContainsPotentiallySensitiveData,
    string ProfileId,
    string ProfileName,
    string RequestCanId,
    string ResponseCanId,
    bool IsExtended,
    bool ProfileMarkedVerified,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<EcuDidReadback> Values);

internal sealed record EcuConfigurationExportEnvelope(
    string Format,
    EcuConfigurationExportDocument Snapshot,
    string PayloadSha256);

/// <summary>
/// Explicit opt-in diagnostic snapshot JSON export for future UI integration.
/// No telemetry, hidden file writes, ECU requests, PINs, security access, or
/// configuration write commands. SHA-256 detects accidental modification;
/// it is not a signature and does not establish the source's authenticity.
/// </summary>
internal static class EcuConfigurationSnapshotExport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    internal const int MaximumValueHexCharacters = 16_384;

    internal static string CreateJson(
        EcuConfigurationReadSnapshot snapshot,
        bool userApprovedSensitiveExport)
    {
        if (!userApprovedSensitiveExport)
            throw new InvalidOperationException(
                "Explicit consent is required to export potentially sensitive vehicle data.");
        ArgumentNullException.ThrowIfNull(snapshot);

        if (string.IsNullOrWhiteSpace(snapshot.ProfileId) || snapshot.ProfileId.Length > 120 ||
            string.IsNullOrWhiteSpace(snapshot.ProfileName) || snapshot.ProfileName.Length > 160)
            throw new InvalidDataException("The snapshot has invalid profile metadata.");

        // The inspector requires a non-default UTC timestamp. Reject an
        // invalid capture BEFORE writing a file that cannot be reopened.
        if (snapshot.CapturedAtUtc == default ||
            snapshot.CapturedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidDataException(
                "The snapshot capture time must be a non-default UTC timestamp.");

        var maxId = snapshot.IsExtended ? 0x1FFFFFFFu : 0x7FFu;
        if (snapshot.RequestId > maxId || snapshot.ResponseId > maxId ||
            snapshot.RequestId == snapshot.ResponseId)
            throw new InvalidDataException("The snapshot has invalid CAN addressing.");

        if (snapshot.Values is null ||
            snapshot.Values.Count is < 1 or > EcuConfigurationReadback.MaximumDataIdentifiers)
            throw new InvalidDataException("The snapshot has an invalid number of DIDs.");

        var seen = new HashSet<ushort>();
        var values = new List<EcuDidReadback>(snapshot.Values.Count);
        foreach (var entry in snapshot.Values)
        {
            if (entry is null || !seen.Add(entry.DataIdentifier) ||
                !IsValidHex(entry.ValueHex))
                throw new InvalidDataException(
                    "The snapshot contains duplicate DIDs or invalid hexadecimal data.");

            values.Add(new EcuDidReadback(entry.DataIdentifier,
                entry.ValueHex.ToUpperInvariant()));
        }

        values.Sort((left, right) => left.DataIdentifier.CompareTo(right.DataIdentifier));
        var document = new EcuConfigurationExportDocument(
            SchemaVersion: 1,
            IsRestorableBackup: false,
            ContainsPotentiallySensitiveData: true,
            snapshot.ProfileId,
            snapshot.ProfileName,
            snapshot.RequestId.ToString(snapshot.IsExtended ? "X8" : "X3"),
            snapshot.ResponseId.ToString(snapshot.IsExtended ? "X8" : "X3"),
            snapshot.IsExtended,
            snapshot.ProfileMarkedVerified,
            snapshot.CapturedAtUtc,
            Array.AsReadOnly(values.ToArray()));

        // Digest covers the exact canonical JSON payload, not the envelope.
        var serializedPayload = JsonSerializer.Serialize(document, JsonOptions);
        var checksum = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(serializedPayload)));

        return JsonSerializer.Serialize(new EcuConfigurationExportEnvelope(
            "dvinholm-diagnostic-did-snapshot", document, checksum), JsonOptions);
    }

    internal static Task SaveToUserSelectedPathAsync(
        EcuConfigurationReadSnapshot snapshot,
        string destinationPath,
        string applicationDirectory,
        bool userApprovedSensitiveExport)
    {
        // Validate consent and data before opening or touching any file.
        var json = CreateJson(snapshot, userApprovedSensitiveExport);
        if (ReportExportDestinationGuard.IsInsideApplicationDirectory(
                destinationPath, applicationDirectory))
            throw new IOException(ReportExportDestinationGuard.BlockedMessage);

        // Reuse the existing verified same-directory temporary-write/rename
        // path so interrupted exports never truncate an older snapshot.
        return ReportExportFileWriter.WriteTextAsync(destinationPath, json);
    }

    private static bool IsValidHex(string? value)
    {
        // The capture path uses Classic CAN ISO-TP, where the UDS
        // positive response header consumes three of 4095 payload bytes.
        // Preserve the larger legacy INSPECTION limit separately: older
        // research JSON remains inspectable, but cannot be newly exported
        // as if it came from the current read-only capture pipeline.
        if (string.IsNullOrEmpty(value) ||
            value.Length > EcuConfigurationReadback.MaximumDidValueBytes * 2 ||
            (value.Length & 1) != 0)
            return false;

        return value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');
    }
}
