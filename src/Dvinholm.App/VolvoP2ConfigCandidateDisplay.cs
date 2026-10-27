using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Text-only, offline presentation of the built-in XC90 P2 research labels.
/// No ECU data, profile, selected CAN ID, file, VIN, diagnostic session or
/// service authorization is accepted as input.
/// </summary>
internal static class VolvoP2ConfigCandidateDisplay
{
    internal static string BuildOverview()
    {
        var text = new StringBuilder();
        text.AppendLine("XC90 P2 configuration candidates (offline research)");
        text.AppendLine();
        text.AppendLine("NOT VEHICLE SETTINGS. The option names below are vendor-advertised");
        text.AppendLine("categories, not verified ECU parameters or a decoded configuration.");
        text.AppendLine("Actual model year, CEM hardware/firmware, bus and option ownership");
        text.AppendLine("are unknown. This viewer neither identifies nor contacts any vehicle.");
        text.AppendLine("SectorV public P2 advertisement (reviewed 2026-10-27) describes");
        text.AppendLine("MY2006+; no individual XC90 variant is independently validated.");
        text.AppendLine();
        text.AppendLine("Current values: unavailable | Desired values: disabled");
        text.AppendLine("PIN/service authentication: unavailable | ECU writing: disabled");
        text.AppendLine();

        foreach (var option in VolvoP2ConfigOptionCatalog.Entries)
        {
            var assessment = VolvoP2ConfigOptionCatalog.Assess(
                option.CanonicalId, modelYear: null,
                modelYearIndependentlyConfirmed: false);
            text.Append("• ").Append(option.DisplayName).Append(" [")
                .Append(option.Group).AppendLine("]");
            text.Append("  Candidate ID: ").AppendLine(option.CanonicalId);
            text.Append("  Claimed model years: ")
                .Append(option.AdvertisedEarliestModelYear)
                .Append("–").AppendLine(option.ScopeLastModelYear.ToString());
            text.Append("  Verification: ")
                .AppendLine(assessment.Kind.ToString());
            text.Append("  Safety/compliance review: ")
                .AppendLine(option.SafetyOrComplianceReviewRequired
                    ? "required before any vehicle operation" : "not yet evaluated");
            text.AppendLine("  Module, firmware, raw mapping: unknown. Read/write disabled.");
            text.AppendLine();
        }

        text.AppendLine("Evidence: https://sectorv.ru/ (advertisement only).");
        text.AppendLine("A real ECU configuration needs verified identity, documented");
        text.AppendLine("mappings, backups, authorization and physical hardware tests.");
        text.AppendLine("This screen cannot request a PIN, read or alter a vehicle.");
        return text.ToString();
    }
}
