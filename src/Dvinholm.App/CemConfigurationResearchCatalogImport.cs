using System.Text;
using System.Text.Json;

namespace Dvinholm.App;

/// <summary>
/// A bounded, user-authored research catalogue. Importing text cannot grant
/// verified CEM identity, a physical option mapping, or ECU access.
/// </summary>
internal sealed record CemConfigurationResearchCatalogDocument(
    string Format,
    int SchemaVersion,
    bool IsUnverifiedResearch,
    bool IsRestorableBackup,
    bool MayAccessEcu,
    IReadOnlyList<CemConfigurationMappingResearchClaim> Claims)
{
    internal bool HasAuthenticatedCemIdentity => false;
    internal bool HasVerifiedParameterMapping => false;
    internal bool MayReadOrWriteVehicle => false;
}

/// <summary>
/// Strict offline JSON intake. Every property must be explicitly listed, every
/// claim is validated using the existing research-only mapping gate, and the
/// caller receives a detached, read-only collection. No original catalog or
/// diagnostic bytes are read, and no executable service is created.
/// </summary>
internal static class CemConfigurationResearchCatalogImport
{
    internal const string Format = "dvinholm-cem-index-mapping-research";
    internal const int MaximumFileBytes = 32 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static CemConfigurationResearchCatalogDocument InspectJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) ||
            StrictUtf8.GetByteCount(json) > MaximumFileBytes)
            throw new InvalidDataException("Missing or oversized CEM research catalog JSON.");

        try
        {
            using var parsed = JsonDocument.Parse(json,
                new JsonDocumentOptions { MaxDepth = 6 });
            var root = parsed.RootElement;
            RequireFields(root, "format", "schemaVersion",
                "isUnverifiedResearch", "isRestorableBackup",
                "mayAccessEcu", "claims");

            if (root.GetProperty("format").ValueKind != JsonValueKind.String ||
                root.GetProperty("format").GetString() != Format ||
                !TryInt(root.GetProperty("schemaVersion"), out var schemaVersion) ||
                schemaVersion != 1 ||
                root.GetProperty("isUnverifiedResearch").ValueKind != JsonValueKind.True ||
                root.GetProperty("isRestorableBackup").ValueKind != JsonValueKind.False ||
                root.GetProperty("mayAccessEcu").ValueKind != JsonValueKind.False)
                throw new InvalidDataException(
                    "Unsupported or unsafe CEM mapping research catalog header.");

            var rows = root.GetProperty("claims");
            if (rows.ValueKind != JsonValueKind.Array ||
                rows.GetArrayLength() is < 1 or > CemConfigurationMappingResearch.MaximumRecords)
                throw new InvalidDataException("CEM mapping catalog must have 1..64 claims.");

            var claims = new List<CemConfigurationMappingResearchClaim>(
                rows.GetArrayLength());
            foreach (var row in rows.EnumerateArray())
            {
                RequireFields(row, "dataFormat", "index1Based",
                    "hardwarePartNumber", "softwareRevision",
                    "claimedMeaning", "evidenceReference");

                var dataFormat = RequireString(row, "dataFormat") switch
                {
                    "P2B9Fc" => CemConfigurationIndexedFormat.P2B9Fc,
                    "P3F106" => CemConfigurationIndexedFormat.P3F106,
                    _ => throw new InvalidDataException(
                        "Unknown P2/P3 CEM research data format.")
                };

                if (!TryInt(row.GetProperty("index1Based"), out var ordinal))
                    throw new InvalidDataException("CEM research index must be an integer.");

                claims.Add(new CemConfigurationMappingResearchClaim(
                    dataFormat, ordinal,
                    RequireString(row, "hardwarePartNumber"),
                    RequireString(row, "softwareRevision"),
                    RequireString(row, "claimedMeaning"),
                    RequireString(row, "evidenceReference")));
            }

            // Validate the ENTIRE imported catalog, not only a requested row.
            // Duplicate/conflicting exact scopes and invalid references fail
            // closed, just as they do for the in-memory research assessor.
            var problems = CemConfigurationMappingResearch.Validate(claims);
            if (problems.Count != 0)
                throw new InvalidDataException(
                    "Invalid CEM research catalog: " + string.Join(", ", problems));

            return new CemConfigurationResearchCatalogDocument(
                Format, 1, true, false, false,
                Array.AsReadOnly(claims.ToArray()));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Malformed CEM research catalog JSON.", ex);
        }
    }

    internal static async Task<CemConfigurationResearchCatalogDocument> InspectFileAsync(
        string userSelectedPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userSelectedPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(Path.GetExtension(userSelectedPath),
                ".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Select an explicit local .json research file.");

        await using var input = new FileStream(userSelectedPath,
            FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var initialSize = input.Length;
        if (initialSize is < 1 or > MaximumFileBytes)
            throw new InvalidDataException("CEM research JSON exceeds its 32 KiB limit.");

        var bytes = new byte[checked((int)initialSize)];
        await input.ReadExactlyAsync(bytes.AsMemory(), cancellationToken);
        if (input.Length != initialSize ||
            await input.ReadAsync(new byte[1], cancellationToken) != 0)
            throw new InvalidDataException("CEM research JSON changed while reading.");

        cancellationToken.ThrowIfCancellationRequested();

        if (bytes.Length >= 3 && bytes[0] == 0xEF &&
            bytes[1] == 0xBB && bytes[2] == 0xBF)
            throw new InvalidDataException("CEM research JSON must be UTF-8 without BOM.");

        try
        {
            return InspectJson(StrictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("CEM research JSON contains invalid UTF-8.", ex);
        }
    }

    internal static string BuildRedactedReport(
        CemConfigurationResearchCatalogDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Format != Format || document.SchemaVersion != 1 ||
            !document.IsUnverifiedResearch || document.IsRestorableBackup ||
            document.MayAccessEcu || document.Claims is null ||
            document.Claims.Count is < 1 or > CemConfigurationMappingResearch.MaximumRecords ||
            CemConfigurationMappingResearch.Validate(document.Claims).Count != 0)
            throw new InvalidDataException("Unsafe or invalid CEM research catalog document.");

        return new StringBuilder()
            .AppendLine("CEM P2/P3 INDEX MAPPING RESEARCH (OFFLINE)")
            .AppendLine("USER-SUPPLIED, UNVERIFIED TEXT CLAIMS; NOT AN OEM CATALOG.")
            .Append("Total claims: ").AppendLine(document.Claims.Count.ToString())
            .Append("P2 claims: ").AppendLine(document.Claims.Count(x =>
                x.Format == CemConfigurationIndexedFormat.P2B9Fc).ToString())
            .Append("P3 claims: ").AppendLine(document.Claims.Count(x =>
                x.Format == CemConfigurationIndexedFormat.P3F106).ToString())
            .AppendLine("Part numbers, software revisions, references and meanings are omitted.")
            .AppendLine("No verified CEM identity, byte meaning, CAN route or live access.")
            .AppendLine("PIN, configuration reads and writes: DISABLED.")
            .ToString();
    }

    private static void RequireFields(JsonElement element, params string[] fields)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("CEM research entry must be an object.");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!names.Add(property.Name) ||
                !fields.Contains(property.Name, StringComparer.Ordinal))
                throw new InvalidDataException("Duplicate or unexpected CEM research property.");

        if (names.Count != fields.Length)
            throw new InvalidDataException("Missing CEM research property.");
    }

    private static string RequireString(JsonElement element, string property)
    {
        var value = element.GetProperty(property);
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("CEM research metadata must be strings.");
        return value.GetString() ?? throw new InvalidDataException(
            "CEM research metadata cannot be null.");
    }

    private static bool TryInt(JsonElement value, out int result)
    {
        result = default;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result);
    }
}
