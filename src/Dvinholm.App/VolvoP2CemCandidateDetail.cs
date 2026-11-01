using System.Text;

namespace Dvinholm.App;

/// <summary>
/// A source-bound, presentation-only detail card for existing CEM research
/// labels. It does not accept a user-provided vehicle identity, parameter
/// value, protocol frame, PIN, credential, or ECU mapping.
/// </summary>
internal static class VolvoP2CemCandidateDetail
{
    internal const int MaximumResearchIdCharacters = 128;

    internal static string BuildReadOnlyReport(
        string? researchId, VolvoP2CemCatalogEvidenceTier tier)
    {
        // Resolve exclusively through the trusted, static and separate
        // evidence catalogs. Never render untrusted identifiers verbatim.
        var matches = VolvoP2CemCatalogSearch.Find(null, tier);
        if (string.IsNullOrWhiteSpace(researchId) ||
            researchId.Length > MaximumResearchIdCharacters ||
            researchId.Any(char.IsControl))
            return Unknown();

        var row = matches.SingleOrDefault(entry =>
            string.Equals(entry.ResearchId, researchId, StringComparison.Ordinal));
        if (row is null)
            return Unknown();

        var report = new StringBuilder()
            .AppendLine("CEM RESEARCH PARAMETER DETAIL (OFFLINE)")
            .Append("Research label: ").AppendLine(row.DisplayName)
            .Append("Dvinholm label ID: ").AppendLine(row.ResearchId)
            .Append("Source tier: ").AppendLine(row.Tier.ToString())
            .AppendLine()
            .AppendLine("WHAT THE SOURCE ESTABLISHES:");

        if (row.Tier == VolvoP2CemCatalogEvidenceTier.VendorAdvertisement)
        {
            var vendor = VolvoP2ConfigOptionCatalog.Entries.Single(entry =>
                string.Equals(entry.CanonicalId, researchId, StringComparison.Ordinal));
            report.AppendLine("A third-party vendor advertised this feature category for P2.")
                .Append("Advertised research scope: MY")
                .Append(vendor.AdvertisedEarliestModelYear)
                .Append('–')
                .AppendLine(vendor.ScopeLastModelYear.ToString())
                .Append("Source (vendor claim only): ").AppendLine(vendor.Source)
                .AppendLine("NOT independently verified for any individual XC90/CEM.");
            if (vendor.SafetyOrComplianceReviewRequired)
                report.AppendLine("Further safety / legal compliance review is required.");
        }
        else
        {
            var oem = VolvoXc90OemCemMenuCatalog.Entries.Single(entry =>
                string.Equals(entry.ResearchId, researchId, StringComparison.Ordinal));
            report.AppendLine("A Volvo XC90 CEM service-menu CATEGORY appears in a")
                .AppendLine("published manufacturer document, not in a live ECU read.")
                .Append("Source: ").AppendLine(oem.ManufacturerDocument)
                .Append("Reference: ").AppendLine(oem.PublicSource)
                .Append("PDF page: ").AppendLine(oem.PdfPage.ToString());
            if (oem.ConditionalEquipment)
                report.AppendLine("Equipment-dependent category; equipment is not confirmed.");
            report.AppendLine("No individual XC90 model year / CEM firmware applicability proven.");
        }

        return report.AppendLine()
            .AppendLine("WHAT IS UNKNOWN (NOT MEASURED):")
            .AppendLine("Physical CEM identity / firmware: UNKNOWN")
            .AppendLine("Module ownership and equipment: UNVERIFIED")
            .AppendLine("Current vehicle setting: UNKNOWN")
            .AppendLine("Value encoding / supported range: UNKNOWN")
            .AppendLine("Diagnostic transport, CAN ID and read mapping: UNKNOWN")
            .AppendLine("Backup / restoration procedure: NOT VERIFIED")
            .AppendLine()
            .AppendLine("AVAILABLE OPERATIONS:")
            .AppendLine("Review this static research label: AVAILABLE OFFLINE")
            .AppendLine("Read CEM configuration: DISABLED")
            .AppendLine("PIN retrieval / authorization: DISABLED")
            .AppendLine("Preview or write vehicle configuration: DISABLED")
            .AppendLine()
            .AppendLine("NEXT EVIDENCE NEEDED:")
            .AppendLine("Independently identify the vehicle model year and exact CEM")
            .AppendLine("hardware / software revision, obtain revision-specific")
            .AppendLine("read-only service documentation and bench acceptance.")
            .AppendLine("A menu name or advertisement never unlocks ECU functions.")
            .AppendLine("No CAN/J2534/UDS request or ECU change occurs here.")
            .AppendLine()
            .Append(VolvoP2CemOptionEvidenceChecklist.BuildReadOnlyReport(row.ResearchId))
            .ToString();
    }

    private static string Unknown() =>
        "No matching research label for this evidence tier. " +
        "No vehicle compatibility or read/write capability established. " +
        "No CAN/J2534 request, PIN or configuration operation.";
}
