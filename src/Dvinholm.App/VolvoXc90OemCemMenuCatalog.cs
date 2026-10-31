namespace Dvinholm.App;

/// <summary>
/// Volvo-documented XC90 service-menu FEATURE NAMES only, sourced from
/// Volvo TJ 20278.6.11 (2016-09-06), XC90 page 3, PDF page 16.
/// These are NOT address/offset values, CEM firmware IDs, diagnostic
/// commands, decoded ECU configuration or authorized changes.
/// </summary>
internal sealed record VolvoXc90DocumentedCemFeature(
    string ResearchId,
    string Name,
    string ManufacturerDocument,
    string PublicSource,
    int PdfPage,
    bool ConditionalEquipment);

internal enum VolvoXc90OemFeatureEvidenceKind
{
    UnknownFeature,
    ManufacturerServiceMenuDocumented,
}

internal sealed record VolvoXc90OemFeatureEvidence(
    VolvoXc90OemFeatureEvidenceKind Kind,
    VolvoXc90DocumentedCemFeature? Feature,
    string Explanation)
{
    internal bool IsExactVehicleOrFirmwareCompatible => false;
    internal bool HasKnownReadMapping => false;
    internal bool HasKnownWriteMapping => false;
    internal bool MayRequestEcu => false;
    internal bool MayEditConfiguration => false;
    internal bool MaySearchOrAuthorizePin => false;
}

/// <summary>
/// Independent manufacturer-document evidence for XC90 CEM service-menu
/// names. This must NOT be silently merged with the eight separate
/// third-party-advertised option categories or promoted to a firmware map.
/// </summary>
internal static class VolvoXc90OemCemMenuCatalog
{
    internal const string Journal = "Volvo Technical Journal 20278.6.11, 2016-09-06";
    internal const string Source =
        "https://static.nhtsa.gov/odi/tsbs/2016/SB-10087303-5448.pdf";
    internal const int SourcePdfPage = 16;

    private static readonly IReadOnlyList<VolvoXc90DocumentedCemFeature> Published =
        Array.AsReadOnly(new[]
        {
            Feature("homesafe-lighting", "Home safe lighting", false),
            Feature("daytime-running-lamps", "Daytime running lamps", false),
            Feature("tailgate-wiper", "Tailgate wiper", true),
            Feature("fuel-filler-lock-delay", "Fuel filler cover locking delay", false),
            Feature("bixenon-lowbeam-position-warning", "Bi-Xenon low-beam position warning", true),
            Feature("three-flash-indicator", "Three-flash turn indicator", false)
        });

    internal static IReadOnlyList<VolvoXc90DocumentedCemFeature> Entries => Published;

    internal static VolvoXc90OemFeatureEvidence Assess(string? researchId)
    {
        var feature = Published.FirstOrDefault(f =>
            string.Equals(f.ResearchId, researchId, StringComparison.Ordinal));

        return feature is null
            ? new(VolvoXc90OemFeatureEvidenceKind.UnknownFeature, null,
                "No matching manufacturer-documented XC90 CEM menu category.")
            : new(VolvoXc90OemFeatureEvidenceKind.ManufacturerServiceMenuDocumented, feature,
                "Volvo lists this category in an XC90 CEM service-settings checklist. " +
                "This is NOT independent proof of availability on a particular model year, " +
                "vehicle/CEM revision or diagnostic interface. No byte offset, " +
                "read/write transport, service authorization or PIN is documented.");
    }

    private static VolvoXc90DocumentedCemFeature Feature(
        string id, string name, bool conditional) =>
        new("xc90.oem.cem." + id, name, Journal, Source, SourcePdfPage, conditional);
}
