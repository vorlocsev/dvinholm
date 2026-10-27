using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App;

/// <summary>
/// Explicitly user-exported, OFFLINE VIDA identity notes. Not a vehicle
/// capture, a verified CEM fingerprint, a restore backup or authorization.
/// No VIN, hardware serial, private notes, PINs or raw DID bytes.
/// </summary>
internal sealed record VolvoP2CemIdentityResearchDocument(
    string Format,
    int SchemaVersion,
    bool IsUnverifiedManualClaim,
    bool IsRestorableBackup,
    bool MayAccessEcu,
    int ClaimedModelYear,
    string ClaimedHardwarePartNumber,
    string ClaimedSoftwarePartNumber,
    string ClaimedDiagnosticSoftwarePartNumber,
    bool UserClaimsVidaSource);

internal static class VolvoP2CemIdentityResearchExport
{
    internal const string Format = "dvinholm-p2-cem-manual-identity-research";
    internal const int MaximumBytes = 4096;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static string CreateJson(string? modelYear, string? hardware,
        string? software, string? diagnostics, bool userClaimsVidaSource)
    {
        var assessment = VolvoP2CemVidaIdentityWorksheet.Assess(
            modelYear, hardware, software, diagnostics, userClaimsVidaSource);
        if (assessment.Kind != VolvoP2CemVidaWorksheetKind.ManualClaimUnverified)
            throw new InvalidDataException("Only complete, validated, unverified VIDA research notes may be saved.");

        // Reuse the worksheet's input verification rather than keeping a
        // second definition of supported year/text syntax in the exporter.
        var year = int.Parse(modelYear!, NumberStyles.None, CultureInfo.InvariantCulture);
        var data = new VolvoP2CemIdentityResearchDocument(
            Format, 1,
            IsUnverifiedManualClaim: true,
            IsRestorableBackup: false,
            MayAccessEcu: false,
            ClaimedModelYear: year,
            ClaimedHardwarePartNumber: hardware!.Trim().ToUpperInvariant(),
            ClaimedSoftwarePartNumber: software!.Trim().ToUpperInvariant(),
            ClaimedDiagnosticSoftwarePartNumber: diagnostics!.Trim().ToUpperInvariant(),
            UserClaimsVidaSource: userClaimsVidaSource);

        var json = JsonSerializer.Serialize(data, JsonOptions);
        if (StrictUtf8.GetByteCount(json) > MaximumBytes)
            throw new InvalidDataException("The CEM research JSON exceeds the export size limit.");
        return json;
    }

    /// <summary>
    /// Parsing a user-made JSON file may NEVER promote its source flag to
    /// physical hardware evidence. Matching metadata cannot grant permissions.
    /// Strict property names and type checks block hidden secret fields.
    /// </summary>
    internal static VolvoP2CemIdentityResearchDocument InspectJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (StrictUtf8.GetByteCount(json) > MaximumBytes)
            throw new InvalidDataException("The CEM research JSON exceeds 4 KiB.");

        try
        {
            using var parsed = JsonDocument.Parse(json,
                new JsonDocumentOptions { MaxDepth = 4 });
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("The CEM research record must be an object.");

            string[] expected =
            [
                "format", "schemaVersion", "isUnverifiedManualClaim",
                "isRestorableBackup", "mayAccessEcu", "claimedModelYear",
                "claimedHardwarePartNumber", "claimedSoftwarePartNumber",
                "claimedDiagnosticSoftwarePartNumber", "userClaimsVidaSource"
            ];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!seen.Add(property.Name) ||
                    !expected.Contains(property.Name, StringComparer.Ordinal))
                    throw new InvalidDataException("Duplicate or unexpected CEM research JSON property.");

            if (seen.Count != expected.Length ||
                root.GetProperty("format").ValueKind != JsonValueKind.String ||
                root.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number ||
                root.GetProperty("claimedModelYear").ValueKind != JsonValueKind.Number ||
                root.GetProperty("claimedHardwarePartNumber").ValueKind != JsonValueKind.String ||
                root.GetProperty("claimedSoftwarePartNumber").ValueKind != JsonValueKind.String ||
                root.GetProperty("claimedDiagnosticSoftwarePartNumber").ValueKind != JsonValueKind.String ||
                root.GetProperty("isUnverifiedManualClaim").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                root.GetProperty("isRestorableBackup").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                root.GetProperty("mayAccessEcu").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                root.GetProperty("userClaimsVidaSource").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidDataException("Missing or incorrectly typed CEM research JSON property.");

            var document = root.Deserialize<VolvoP2CemIdentityResearchDocument>(JsonOptions)
                ?? throw new InvalidDataException("Invalid CEM research document.");
            if (document.Format != Format ||
                document.SchemaVersion != 1 ||
                !document.IsUnverifiedManualClaim ||
                document.IsRestorableBackup || document.MayAccessEcu)
                throw new InvalidDataException("Unsupported format or unsafe CEM research capabilities.");

            // A file is an untrusted, independently editable text claim.
            // Revalidate all typed values, never trust its origin assertion.
            var assessment = VolvoP2CemVidaIdentityWorksheet.Assess(
                document.ClaimedModelYear.ToString(CultureInfo.InvariantCulture),
                document.ClaimedHardwarePartNumber,
                document.ClaimedSoftwarePartNumber,
                document.ClaimedDiagnosticSoftwarePartNumber,
                document.UserClaimsVidaSource);
            if (assessment.Kind != VolvoP2CemVidaWorksheetKind.ManualClaimUnverified ||
                document.ClaimedHardwarePartNumber != document.ClaimedHardwarePartNumber.Trim().ToUpperInvariant() ||
                document.ClaimedSoftwarePartNumber != document.ClaimedSoftwarePartNumber.Trim().ToUpperInvariant() ||
                document.ClaimedDiagnosticSoftwarePartNumber != document.ClaimedDiagnosticSoftwarePartNumber.Trim().ToUpperInvariant())
                throw new InvalidDataException("Invalid or noncanonical manual CEM part-number fields.");

            return document;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Malformed CEM research JSON.", ex);
        }
    }

    internal static Task SaveToUserSelectedPathAsync(
        string? modelYear, string? hardware, string? software,
        string? diagnostics, bool userClaimsVidaSource,
        string destinationPath, string applicationDirectory)
    {
        // First validate the complete record and target path; never create a
        // file on a validation failure or cancelled path-only picker.
        var json = CreateJson(modelYear, hardware, software, diagnostics,
            userClaimsVidaSource);
        if (ReportExportDestinationGuard.IsInsideApplicationDirectory(
            destinationPath, applicationDirectory))
            throw new IOException(ReportExportDestinationGuard.BlockedMessage);

        return ReportExportFileWriter.WriteTextAsync(destinationPath, json);
    }
}
