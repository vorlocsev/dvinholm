using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// A privacy-reduced offline research view linking two independently
/// user-selected and inspected files. This is NOT a cross-validated
/// CEM fingerprint, diagnostic transport, decoded configuration or backup.
/// </summary>
internal static class VolvoP2CemPairedResearchIntakeDisplay
{
    internal static string Build(
        VolvoP2CemIdentityResearchDocument claimedIdentity,
        EcuConfigurationExportDocument historicalSnapshot)
    {
        // A valid-looking, user-editable part-number card never becomes
        // trusted merely because its data is shown beside a DID capture.
        VolvoP2CemIdentityResearchFileInspection.BuildReadOnlyReview(claimedIdentity);
        if (!Canonical(claimedIdentity.ClaimedHardwarePartNumber) ||
            !Canonical(claimedIdentity.ClaimedSoftwarePartNumber) ||
            !Canonical(claimedIdentity.ClaimedDiagnosticSoftwarePartNumber))
            throw new InvalidDataException("Noncanonical CEM research part number.");

        // This existing validator omits raw DID bytes, VIN, profile data and
        // local paths. It also rejects malformed transport/DID metadata.
        var redactedSnapshot = EcuCemResearchIntakeDisplay.Build(historicalSnapshot);
        var text = new StringBuilder()
            .AppendLine("Dvinholm XC90 P2/CEM | PAIRED OFFLINE RESEARCH INTAKE")
            .AppendLine("UNVERIFIED: TWO USER-SELECTED FILES, NOT A LINK TO ONE PHYSICAL MODULE.")
            .AppendLine("NO AUTOMATIC MATCHING. The diagnostic snapshot might belong to another ECU or car.")
            .AppendLine("Neither a claimed VIDA source nor a checksum authenticates a physical CEM.")
            .AppendLine()
            .AppendLine("MANUALLY CLAIMED VIDA IDENTITY (unverified)")
            .Append("  XC90 model year (claimed): ")
            .AppendLine(claimedIdentity.ClaimedModelYear.ToString(CultureInfo.InvariantCulture))
            .Append("  Hardware P/N (claimed): ")
            .AppendLine(claimedIdentity.ClaimedHardwarePartNumber)
            .Append("  Software P/N (claimed): ")
            .AppendLine(claimedIdentity.ClaimedSoftwarePartNumber)
            .Append("  Diagnostic software P/N (claimed): ")
            .AppendLine(claimedIdentity.ClaimedDiagnosticSoftwarePartNumber)
            .Append("  VIDA provenance: ")
            .AppendLine(claimedIdentity.UserClaimsVidaSource
                ? "USER CLAIM ONLY, not independently authenticated"
                : "NOT CLAIMED; origin unknown")
            .AppendLine()
            .AppendLine("SEPARATE HISTORICAL DIAGNOSTIC OBSERVATION")
            .AppendLine(redactedSnapshot)
            .AppendLine("PAIR ASSESSMENT: UNCORRELATED RESEARCH CLAIMS ONLY.")
            .AppendLine("No DID-to-part-number meaning, physical CEM address, vehicle association,")
            .AppendLine("model-year match, transport support or firmware applicability is inferred.")
            .AppendLine("Raw DID values, VIN, profile names, serials, PINs and paths are not included.")
            .AppendLine("No file changes, J2534/CAN request, PIN operation, service authorization,")
            .AppendLine("configuration read/write or backup/recovery capability is enabled.")
            .AppendLine("Part numbers, CAN IDs and DID lengths can still fingerprint a vehicle.")
            .AppendLine("Inspect this report before any intentional sharing.");
        return text.ToString();
    }

    private static bool Canonical(string? value) =>
        value is not null &&
        string.Equals(value, value.Trim().ToUpperInvariant(), StringComparison.Ordinal);
}
