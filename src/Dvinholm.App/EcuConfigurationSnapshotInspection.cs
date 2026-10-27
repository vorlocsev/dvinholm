using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App;

/// <summary>
/// Inspect a previously exported diagnostic snapshot, without turning
/// untrusted file data into live ECU evidence or restorable configuration.
/// SHA-256 detects corruption only; a modified file can be rehashed.
/// </summary>
internal static class EcuConfigurationSnapshotInspection
{
    internal const int MaximumFileBytes = 4 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static EcuConfigurationExportDocument InspectJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (Encoding.UTF8.GetByteCount(json) > MaximumFileBytes)
            throw new InvalidDataException("The diagnostic snapshot exceeds the 4 MiB inspection limit.");

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            RequireFields(root, "format", "snapshot", "payloadSha256");

            if (root.GetProperty("format").GetString() != "dvinholm-diagnostic-did-snapshot")
                throw new InvalidDataException("Unrecognized diagnostic snapshot format.");

            var snapshot = root.GetProperty("snapshot");
            RequireFields(snapshot, "schemaVersion", "isRestorableBackup",
                "containsPotentiallySensitiveData", "profileId", "profileName",
                "requestCanId", "responseCanId", "isExtended",
                "profileMarkedVerified", "capturedAtUtc", "values");

            // Reject re-used or reordered properties, surprise nested fields,
            // aliases and incomplete DID records before interpreting values.
            var entries = snapshot.GetProperty("values");
            if (entries.ValueKind != JsonValueKind.Array ||
                entries.GetArrayLength() is < 1 or > EcuConfigurationReadback.MaximumDataIdentifiers)
                throw new InvalidDataException("Invalid diagnostic DID count.");
            foreach (var entry in entries.EnumerateArray())
                RequireFields(entry, "dataIdentifier", "valueHex");

            var digestText = root.GetProperty("payloadSha256").GetString();
            if (!IsUppercaseHex(digestText, 64))
                throw new InvalidDataException("Missing or invalid snapshot SHA-256.");
            var expectedDigest = Convert.FromHexString(digestText!);
            var actualDigest = SHA256.HashData(StrictUtf8.GetBytes(snapshot.GetRawText()));
            if (!CryptographicOperations.FixedTimeEquals(expectedDigest, actualDigest))
                throw new InvalidDataException("Snapshot SHA-256 mismatch; the file may have changed.");

            var result = snapshot.Deserialize<EcuConfigurationExportDocument>(JsonOptions)
                ?? throw new InvalidDataException("Missing diagnostic snapshot data.");
            if (result.SchemaVersion != 1 || result.IsRestorableBackup ||
                !result.ContainsPotentiallySensitiveData)
                throw new InvalidDataException("Unsupported diagnostic snapshot schema or safety flags.");

            if (string.IsNullOrWhiteSpace(result.ProfileId) || result.ProfileId.Length > 120 ||
                string.IsNullOrWhiteSpace(result.ProfileName) || result.ProfileName.Length > 160)
                throw new InvalidDataException("Invalid diagnostic snapshot profile.");

            var width = result.IsExtended ? 8 : 3;
            var maxId = result.IsExtended ? 0x1FFFFFFFu : 0x7FFu;
            if (!IsUppercaseHex(result.RequestCanId, width) ||
                !IsUppercaseHex(result.ResponseCanId, width) ||
                !uint.TryParse(result.RequestCanId, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var requestId) ||
                !uint.TryParse(result.ResponseCanId, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var responseId) ||
                requestId > maxId || responseId > maxId || requestId == responseId)
                throw new InvalidDataException("Invalid diagnostic snapshot CAN addressing.");

            if (result.CapturedAtUtc == default ||
                result.CapturedAtUtc.Offset != TimeSpan.Zero)
                throw new InvalidDataException("Diagnostic snapshot capture time must be UTC.");

            if (result.Values is null || result.Values.Count != entries.GetArrayLength())
                throw new InvalidDataException("Invalid diagnostic DID records.");
            var seen = new HashSet<ushort>();
            ushort? previousDid = null;
            foreach (var entry in result.Values)
            {
                if (entry is null || !seen.Add(entry.DataIdentifier) ||
                    (previousDid is not null && entry.DataIdentifier <= previousDid.Value) ||
                    !IsUppercaseHexValue(entry.ValueHex))
                    throw new InvalidDataException("Unsorted, duplicate or invalid diagnostic DID value.");
                previousDid = entry.DataIdentifier;
            }

            // Even after a matching checksum, caller receives only
            // historical, read-only data, NOT authorization or a live capture.
            return result with { Values = Array.AsReadOnly(result.Values.ToArray()) };
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Malformed diagnostic snapshot JSON.", ex);
        }
    }

    internal static async Task<EcuConfigurationExportDocument> InspectFileAsync(
        string userSelectedPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userSelectedPath);
        cancellationToken.ThrowIfCancellationRequested();
        await using var input = new FileStream(userSelectedPath, FileMode.Open,
            FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (input.Length is < 1 or > MaximumFileBytes)
            throw new InvalidDataException("Diagnostic snapshot file is empty or exceeds 4 MiB.");

        var bytes = new byte[(int)input.Length];
        await input.ReadExactlyAsync(bytes, cancellationToken);
        var extra = new byte[1];
        if (await input.ReadAsync(extra, cancellationToken) != 0)
            throw new InvalidDataException("Diagnostic snapshot size changed while reading.");

        try
        {
            return InspectJson(StrictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("Diagnostic snapshot must contain valid UTF-8.", ex);
        }
    }

    private static void RequireFields(JsonElement item, params string[] names)
    {
        if (item.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Invalid diagnostic snapshot object.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in item.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !names.Contains(property.Name, StringComparer.Ordinal))
                throw new InvalidDataException("Duplicate or unexpected diagnostic snapshot JSON property.");
        }
        if (seen.Count != names.Length)
            throw new InvalidDataException("Incomplete diagnostic snapshot JSON object.");
    }

    private static bool IsUppercaseHex(string? value, int length) =>
        value is not null && value.Length == length &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static bool IsUppercaseHexValue(string? value) =>
        value is { Length: >= 2 and <= EcuConfigurationSnapshotExport.MaximumValueHexCharacters } &&
        (value.Length & 1) == 0 &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
