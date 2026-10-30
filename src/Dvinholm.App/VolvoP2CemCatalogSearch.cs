using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Evidence classes are *separate*: an OEM service-menu name does not verify
/// the vendor's category or confer permission to use a real CEM service.
/// </summary>
internal enum VolvoP2CemCatalogEvidenceTier
{
    All,
    VendorAdvertisement,
    VolvoPublishedServiceMenu
}

internal sealed record VolvoP2CemCatalogSearchRow(
    string ResearchId,
    string DisplayName,
    string Category,
    VolvoP2CemCatalogEvidenceTier Tier,
    string ClaimedScope,
    bool NeedsSafetyOrEquipmentReview)
{
    internal bool HasAuthenticatedVehicleOrFirmware => false;
    internal bool MayReadCem => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteCem => false;
}

/// <summary>
/// Text-only, stateless explorer over 8 vendor-advertised feature labels and
/// 6 separately documented Volvo service-menu names. No ECU, CAN, J2534,
/// VIN, PIN, credentials, raw configuration values or session are involved.
/// </summary>
internal static class VolvoP2CemCatalogSearch
{
    internal const int MaximumQueryCharacters = 80;

    internal static IReadOnlyList<VolvoP2CemCatalogSearchRow> Find(
        string? query, VolvoP2CemCatalogEvidenceTier tier)
    {
        if (!Enum.IsDefined(tier))
            throw new InvalidDataException("Unknown CEM catalog evidence tier.");

        // The query is never reported, persisted, transmitted or added to logs.
        if (query is { Length: > MaximumQueryCharacters } ||
            query is not null && query.Any(char.IsControl))
            throw new InvalidDataException("Invalid CEM catalog search text.");

        var terms = (query ?? string.Empty).Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rows = new List<VolvoP2CemCatalogSearchRow>(14);

        if (tier is VolvoP2CemCatalogEvidenceTier.All or
            VolvoP2CemCatalogEvidenceTier.VendorAdvertisement)
        {
            foreach (var option in VolvoP2ConfigOptionCatalog.Entries)
                rows.Add(new(option.CanonicalId, option.DisplayName,
                    option.Group.ToString(),
                    VolvoP2CemCatalogEvidenceTier.VendorAdvertisement,
                    "Vendor-advertised P2 model years 2006–2014 only",
                    option.SafetyOrComplianceReviewRequired));
        }

        if (tier is VolvoP2CemCatalogEvidenceTier.All or
            VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu)
        {
            foreach (var feature in VolvoXc90OemCemMenuCatalog.Entries)
                rows.Add(new(feature.ResearchId, feature.Name,
                    "XC90 CEM service menu",
                    VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu,
                    "XC90 service-menu category only; no vehicle-year or revision proof",
                    feature.ConditionalEquipment));
        }

        // Every term must occur in one of the displayed research fields.
        // No synonyms, inferred platform scope or hidden parameter mapping.
        return Array.AsReadOnly(rows.Where(row =>
            terms.All(term =>
                row.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                row.ResearchId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                row.Category.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToArray());
    }

    internal static string BuildReadOnlyReport(
        string? query, VolvoP2CemCatalogEvidenceTier tier)
    {
        var rows = Find(query, tier);
        var report = new StringBuilder()
            .AppendLine("XC90 P2/CEM CANDIDATE FINDER (OFFLINE)")
            .Append("Found ").Append(rows.Count)
            .Append(" research labels (no live ECU scan).").AppendLine()
            .AppendLine("Vendor advertisements and Volvo service-menu names are DIFFERENT evidence tiers.");

        if (rows.Count == 0)
            report.AppendLine("No matching research labels. No compatibility conclusion.");

        foreach (var row in rows)
        {
            report.Append("• [").Append(row.Tier).Append("] ")
                .AppendLine(row.DisplayName)
                .Append("  Category: ").AppendLine(row.Category)
                .Append("  Dvinholm research ID: ").AppendLine(row.ResearchId)
                .Append("  Source scope: ").AppendLine(row.ClaimedScope);
            if (row.NeedsSafetyOrEquipmentReview)
                report.AppendLine("  Additional safety, compliance or equipment review may be required.");
            report.AppendLine("  Current value: UNKNOWN | Read mapping: UNKNOWN | Write: DISABLED");
        }

        return report.AppendLine("No model year, VIN, firmware identity or CEM hardware was authenticated.")
            .AppendLine("Names cannot establish ECU ownership, transport, real values or support.")
            .AppendLine("No CAN/J2534 requests, PIN access, configuration read/write or backup.")
            .ToString();
    }
}
