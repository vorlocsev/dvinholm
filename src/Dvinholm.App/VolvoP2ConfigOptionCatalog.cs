namespace Dvinholm.App;

/// <summary>
/// User-facing groups of *advertised candidate* P2 settings, not an ECU
/// parameter address space, a DID catalog, or a list of supported writes.
/// </summary>
internal enum VolvoP2ConfigOptionGroup
{
    AuxiliaryHeating,
    ExteriorLighting,
    Region,
    ParkingAssistance,
    LockingAndAlarm,
    Maintenance,
    WheelAndTyre,
    VehicleSpeedPolicy
}

internal enum VolvoP2ConfigCandidateAssessmentKind
{
    NotListed,
    ModelYearUnconfirmed,
    OutsideFirstGenerationXc90Scope,
    OutsideVendorAdvertisedModelYears,
    VendorAdvertisedOnly
}

/// <summary>
/// Pure UI/research metadata. There are deliberately NO raw byte offsets,
/// CAN IDs, DIDs, parameter values, PINs, ECU commands or write encodings.
/// Responsibility for the option must be established per CEM revision.
/// </summary>
internal sealed record VolvoP2ConfigOptionCandidate(
    string CanonicalId,
    string DisplayName,
    VolvoP2ConfigOptionGroup Group,
    int AdvertisedEarliestModelYear,
    int ScopeLastModelYear,
    string Source,
    bool SafetyOrComplianceReviewRequired)
{
    internal bool ModuleOwnershipVerified => false;
    internal bool FirmwareApplicabilityVerified => false;
    internal bool HasVerifiedReadMapping => false;
    internal bool HasVerifiedWriteMapping => false;
    internal bool CanReadFromVehicle => false;
    internal bool CanWriteToVehicle => false;
    internal bool CanRetrieveOrAuthorizePin => false;
}

internal sealed record VolvoP2ConfigCandidateAssessment(
    VolvoP2ConfigCandidateAssessmentKind Kind,
    VolvoP2ConfigOptionCandidate? Candidate,
    string Explanation)
{
    internal bool CanIssueVehicleRequest => false;
    internal bool CanEditVehicle => false;
    internal bool CanAuthorizeServiceOrPin => false;
}

/// <summary>
/// XC90 P2 candidate option *names* derived from explicitly attributed
/// vendor-advertised feature categories on 2026-10-27. Neither SectorV's
/// unpublished data nor any proprietary option IDs are included.
/// A vendor advertisement is not independent firmware/hardware evidence.
/// </summary>
internal static class VolvoP2ConfigOptionCatalog
{
    internal const int FirstFirstGenerationModelYear = 2003;
    internal const int LastFirstGenerationModelYear = 2014;
    internal const int VendorAdvertisedEarliestP2ModelYear = 2006;

    private const string VendorAdvertisementSource =
        "SectorV public P2 features, vendor advertisement observed 2026-10-27, " +
        "not independently verified: https://sectorv.ru/";

    // The IDs below are NEW internal Dvinholm labels, not factory codes or
    // any external application's parameter IDs.
    private static readonly IReadOnlyList<VolvoP2ConfigOptionCandidate> Candidates =
        Array.AsReadOnly(new[]
        {
            Candidate("p2.xc90.auxiliary-heater", "Auxiliary heater",
                VolvoP2ConfigOptionGroup.AuxiliaryHeating, true),
            Candidate("p2.xc90.headlamp-washers", "Headlamp washers",
                VolvoP2ConfigOptionGroup.ExteriorLighting, true),
            Candidate("p2.xc90.market-region", "Market / region",
                VolvoP2ConfigOptionGroup.Region, true),
            Candidate("p2.xc90.parking-assistance", "Parking assistance",
                VolvoP2ConfigOptionGroup.ParkingAssistance, true),
            Candidate("p2.xc90.automatic-locking", "Automatic locking",
                VolvoP2ConfigOptionGroup.LockingAndAlarm, true),
            Candidate("p2.xc90.service-interval", "Service interval",
                VolvoP2ConfigOptionGroup.Maintenance, false),
            Candidate("p2.xc90.wheel-size", "Wheel size",
                VolvoP2ConfigOptionGroup.WheelAndTyre, true),
            Candidate("p2.xc90.speed-policy", "Speed limit policy",
                VolvoP2ConfigOptionGroup.VehicleSpeedPolicy, true)
        });

    internal static IReadOnlyList<VolvoP2ConfigOptionCandidate> Entries => Candidates;

    /// <summary>
    /// Checks research presentation scope only. Even a matching 2006-2014
    /// year is never evidence that an individual XC90/CEM exposes this option.
    /// </summary>
    internal static VolvoP2ConfigCandidateAssessment Assess(
        string? canonicalId,
        int? modelYear,
        bool modelYearIndependentlyConfirmed)
    {
        var candidate = Candidates.FirstOrDefault(entry =>
            string.Equals(entry.CanonicalId, canonicalId, StringComparison.Ordinal));
        if (candidate is null)
            return new(VolvoP2ConfigCandidateAssessmentKind.NotListed, null,
                "Option is absent from this independent Dvinholm research catalog.");

        if (modelYear is null || !modelYearIndependentlyConfirmed)
            return new(VolvoP2ConfigCandidateAssessmentKind.ModelYearUnconfirmed,
                candidate, "Vehicle model year is unconfirmed; no applicability is established.");

        if (modelYear.Value < FirstFirstGenerationModelYear ||
            modelYear.Value > LastFirstGenerationModelYear)
            return new(VolvoP2ConfigCandidateAssessmentKind.OutsideFirstGenerationXc90Scope,
                candidate, "Model year is outside the first-generation XC90 research scope.");

        if (modelYear.Value < candidate.AdvertisedEarliestModelYear ||
            modelYear.Value > candidate.ScopeLastModelYear)
            return new(VolvoP2ConfigCandidateAssessmentKind.OutsideVendorAdvertisedModelYears,
                candidate, "This model year falls outside the cited vendor's P2 advertising scope.");

        return new(VolvoP2ConfigCandidateAssessmentKind.VendorAdvertisedOnly, candidate,
            "Candidate display category only. ECU ownership, exact hardware/software revision, " +
            "parameter mapping, dependencies, PIN and read/write capability are not verified.");
    }

    private static VolvoP2ConfigOptionCandidate Candidate(
        string id, string name, VolvoP2ConfigOptionGroup group, bool review) =>
        new(id, name, group, VendorAdvertisedEarliestP2ModelYear,
            LastFirstGenerationModelYear, VendorAdvertisementSource, review);
}
