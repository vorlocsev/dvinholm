using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App;

/// <summary>User-editable CEM revision source claim, never authenticated ECU evidence.</summary>
internal sealed record VolvoP2CemRevisionSourceClaim(
    string Format, int SchemaVersion,
    bool IsUnverifiedResearchClaim, bool IsRestorableBackup, bool MayAccessEcu,
    int ModelYearFrom, int ModelYearThrough,
    string ClaimedHardwarePartNumber, string ClaimedSoftwarePartNumber,
    string ClaimedDiagnosticSoftwarePartNumber, string SourceReference);

internal enum VolvoP2CemRevisionSourceMatchKind
{
    YearOutsideResearchScope,
    ClaimedPartNumbersDiffer,
    SameUnverifiedPartNumberClaims
}

internal sealed record VolvoP2CemRevisionSourceMatch(
    VolvoP2CemRevisionSourceMatchKind Kind,
    bool HardwareClaimMatches, bool SoftwareClaimMatches,
    bool DiagnosticSoftwareClaimMatches)
{
    internal bool HasAuthenticatedCemIdentity => false;
    internal bool HasVerifiedDiagnosticAddressing => false;
    internal bool HasVerifiedConfigurationMapping => false;
    internal bool MayConnectOrReadCem => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
}

/// <summary>
/// Offline comparison of two unverified files. A full textual match never
/// establishes physical CEM identity or any executable service capability.
/// </summary>
internal static class VolvoP2CemRevisionSourceComparison
{
    internal const string Format = "dvinholm-p2-cem-revision-source-claim";
    internal const int MaximumFileBytes = 4096;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static VolvoP2CemRevisionSourceClaim InspectJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || StrictUtf8.GetByteCount(json) > MaximumFileBytes)
            throw new InvalidDataException("Missing or oversized CEM revision source claim.");

        try
        {
            using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("CEM source claim must be a JSON object.");

            string[] expected =
            [
                "format", "schemaVersion", "isUnverifiedResearchClaim",
                "isRestorableBackup", "mayAccessEcu", "modelYearFrom",
                "modelYearThrough", "claimedHardwarePartNumber",
                "claimedSoftwarePartNumber", "claimedDiagnosticSoftwarePartNumber",
                "sourceReference"
            ];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!seen.Add(property.Name) ||
                    !expected.Contains(property.Name, StringComparer.Ordinal))
                    throw new InvalidDataException("Duplicate or unexpected CEM source claim property.");

            if (seen.Count != expected.Length ||
                root.GetProperty("format").ValueKind != JsonValueKind.String ||
                root.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number ||
                root.GetProperty("isUnverifiedResearchClaim").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                root.GetProperty("isRestorableBackup").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                root.GetProperty("mayAccessEcu").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                root.GetProperty("modelYearFrom").ValueKind != JsonValueKind.Number ||
                root.GetProperty("modelYearThrough").ValueKind != JsonValueKind.Number ||
                root.GetProperty("claimedHardwarePartNumber").ValueKind != JsonValueKind.String ||
                root.GetProperty("claimedSoftwarePartNumber").ValueKind != JsonValueKind.String ||
                root.GetProperty("claimedDiagnosticSoftwarePartNumber").ValueKind != JsonValueKind.String ||
                root.GetProperty("sourceReference").ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Missing or incorrectly typed source claim property.");

            var claim = root.Deserialize<VolvoP2CemRevisionSourceClaim>(JsonOptions)
                ?? throw new InvalidDataException("Invalid CEM source claim.");
            ValidateClaim(claim);
            return claim;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Malformed CEM revision source claim JSON.", ex);
        }
    }

    internal static async Task<VolvoP2CemRevisionSourceClaim> InspectFileAsync(
        string selectedPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = new FileStream(selectedPath, FileMode.Open,
            FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is < 1 or > MaximumFileBytes)
            throw new InvalidDataException("CEM source claim JSON must be 1..4096 bytes.");

        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        var extra = new byte[1];
        if (await stream.ReadAsync(extra, cancellationToken) != 0)
            throw new InvalidDataException("CEM source claim file size changed while reading.");
        if (bytes.Length >= 3 && bytes[0] == 0xEF &&
            bytes[1] == 0xBB && bytes[2] == 0xBF)
            throw new InvalidDataException("Source claim must be UTF-8 without BOM.");

        try
        {
            return InspectJson(StrictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("Source claim must be valid UTF-8.", ex);
        }
    }

    internal static VolvoP2CemRevisionSourceMatch Compare(
        VolvoP2CemIdentityResearchDocument manualCard,
        VolvoP2CemRevisionSourceClaim sourceClaim)
    {
        ValidateManualCard(manualCard);
        ValidateClaim(sourceClaim);
        if (manualCard.ClaimedModelYear < sourceClaim.ModelYearFrom ||
            manualCard.ClaimedModelYear > sourceClaim.ModelYearThrough)
            return new(VolvoP2CemRevisionSourceMatchKind.YearOutsideResearchScope,
                false, false, false);

        var hw = string.Equals(manualCard.ClaimedHardwarePartNumber,
            sourceClaim.ClaimedHardwarePartNumber, StringComparison.Ordinal);
        var sw = string.Equals(manualCard.ClaimedSoftwarePartNumber,
            sourceClaim.ClaimedSoftwarePartNumber, StringComparison.Ordinal);
        var diag = string.Equals(manualCard.ClaimedDiagnosticSoftwarePartNumber,
            sourceClaim.ClaimedDiagnosticSoftwarePartNumber, StringComparison.Ordinal);
        return new(hw && sw && diag
                ? VolvoP2CemRevisionSourceMatchKind.SameUnverifiedPartNumberClaims
                : VolvoP2CemRevisionSourceMatchKind.ClaimedPartNumbersDiffer,
            hw, sw, diag);
    }

    internal static string BuildReadOnlyReport(
        VolvoP2CemIdentityResearchDocument manualCard,
        VolvoP2CemRevisionSourceClaim sourceClaim)
    {
        var result = Compare(manualCard, sourceClaim);
        var report = new StringBuilder()
            .AppendLine("XC90 P2/CEM REVISION SOURCE CROSS-CHECK (OFFLINE)")
            .AppendLine("TWO UNVERIFIED USER-SELECTED CLAIMS, NOT AN ECU IDENTIFICATION.")
            .Append("Manually claimed model year: ")
            .AppendLine(manualCard.ClaimedModelYear.ToString(CultureInfo.InvariantCulture))
            .Append("Claimed source year scope: ")
            .Append(sourceClaim.ModelYearFrom.ToString(CultureInfo.InvariantCulture))
            .Append("..").AppendLine(sourceClaim.ModelYearThrough.ToString(CultureInfo.InvariantCulture))
            .Append("Research classification: ").AppendLine(result.Kind.ToString());
        if (result.Kind == VolvoP2CemRevisionSourceMatchKind.YearOutsideResearchScope)
        {
            report.AppendLine("Comparison withheld: claimed model year is outside source scope.");
        }
        else
        {
            report.Append("Hardware P/N CLAIM: ")
                .AppendLine(result.HardwareClaimMatches ? "SAME" : "DIFFERENT")
                .Append("Software P/N CLAIM: ")
                .AppendLine(result.SoftwareClaimMatches ? "SAME" : "DIFFERENT")
                .Append("Diagnostic software P/N CLAIM: ")
                .AppendLine(result.DiagnosticSoftwareClaimMatches ? "SAME" : "DIFFERENT");
        }

        return report.AppendLine("A match proves neither source authenticity, identical physical CEM,")
            .AppendLine("firmware compatibility, CAN/K-Line transport nor configuration mapping.")
            .AppendLine("Source references, part numbers, VIN/PIN, file paths and raw DIDs are withheld.")
            .AppendLine("No profile, file, adapter or vehicle changes; no J2534/UDS request.")
            .AppendLine("PIN retrieval, key programming, CEM configuration read/write: DISABLED.")
            .ToString();
    }

    private static void ValidateManualCard(VolvoP2CemIdentityResearchDocument card)
    {
        if (card is null || card.Format != VolvoP2CemIdentityResearchExport.Format ||
            card.SchemaVersion != 1 || !card.IsUnverifiedManualClaim ||
            card.IsRestorableBackup || card.MayAccessEcu ||
            !ValidPartClaims(card.ClaimedModelYear, card.ClaimedHardwarePartNumber,
                card.ClaimedSoftwarePartNumber, card.ClaimedDiagnosticSoftwarePartNumber))
            throw new InvalidDataException("Invalid or unsafe manual CEM identity research claim.");
    }

    private static void ValidateClaim(VolvoP2CemRevisionSourceClaim claim)
    {
        if (claim is null || claim.Format != Format || claim.SchemaVersion != 1 ||
            !claim.IsUnverifiedResearchClaim || claim.IsRestorableBackup ||
            claim.MayAccessEcu ||
            claim.ModelYearFrom < VolvoP2CemVidaIdentityWorksheet.FirstGenerationXc90EarliestModelYear ||
            claim.ModelYearThrough > VolvoP2CemVidaIdentityWorksheet.FirstGenerationXc90LastModelYear ||
            claim.ModelYearFrom > claim.ModelYearThrough ||
            !ValidPartClaims(claim.ModelYearFrom, claim.ClaimedHardwarePartNumber,
                claim.ClaimedSoftwarePartNumber, claim.ClaimedDiagnosticSoftwarePartNumber) ||
            claim.SourceReference is not { Length: >= 12 and <= 256 } ||
            claim.SourceReference.Any(c => c < ' ' || c > '~'))
            throw new InvalidDataException("Invalid or unsafe CEM revision source claim.");
    }

    private static bool ValidPartClaims(int year, string hardware,
        string software, string diagnostics)
    {
        if (hardware is null || software is null || diagnostics is null)
            return false;
        var assessment = VolvoP2CemVidaIdentityWorksheet.Assess(
            year.ToString(CultureInfo.InvariantCulture),
            hardware, software, diagnostics, false);
        return assessment.Kind == VolvoP2CemVidaWorksheetKind.ManualClaimUnverified &&
            IsCanonical(hardware) && IsCanonical(software) && IsCanonical(diagnostics);
    }

    private static bool IsCanonical(string value) =>
        value == value.Trim().ToUpperInvariant();
}
