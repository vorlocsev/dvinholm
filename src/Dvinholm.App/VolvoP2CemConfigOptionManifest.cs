namespace Dvinholm.App;

/// <summary>
/// Where a display-only XC90/P2 option name was observed. These provenance
/// tiers NEVER authenticate a vehicle, CEM revision or configuration mapping.
/// </summary>
internal enum VolvoP2CemOptionProvenance
{
    ThirdPartyAdvertisement,
    VolvoPublishedCemServiceMenu
}

/// <summary>
/// Future-editor metadata for an option label. Null physical fields mean
/// UNKNOWN, not unrestricted. In particular, an OEM CEM menu label is NOT
/// evidence of byte ownership, valid values or model-year applicability.
/// </summary>
internal sealed record VolvoP2CemConfigOptionDefinition(
    string CanonicalId,
    string DisplayName,
    string Category,
    VolvoP2CemOptionProvenance Provenance,
    string EvidenceReference,
    int? ClaimedModelYearFrom,
    int? ClaimedModelYearThrough,
    bool AdditionalSafetyOrEquipmentReviewRequired,
    string? VerifiedPhysicalModuleOwner,
    string? VerifiedCemHardwareRevision,
    string? VerifiedCemSoftwareRevision,
    string? VerifiedRawValueMapping,
    IReadOnlyList<string> VerifiedAllowedValues,
    IReadOnlyList<string> VerifiedDependencies)
{
    // The display-only manifest intentionally has no executable capability.
    internal bool HasVerifiedPhysicalIdentity => false;
    internal bool HasVerifiedConfigurationReadMapping => false;
    internal bool HasVerifiedConfigurationWriteMapping => false;
    internal bool MayReadVehicleConfiguration => false;
    internal bool MayEditVehicleConfiguration => false;
    internal bool MayAcquirePinOrAuthenticate => false;
    internal bool MayTransmitToVehicle => false;
}

/// <summary>
/// Single immutable, source-separated option-definition manifest for a
/// future P2 configuration editor. It reuses the existing eight advertised
/// candidates and six manufacturer menu labels, rather than duplicating
/// evidence or converting an OEM menu into a writable parameter.
/// </summary>
internal static class VolvoP2CemConfigOptionManifest
{
    private static readonly IReadOnlyList<string> NoVerifiedValues =
        Array.AsReadOnly(Array.Empty<string>());

    private static readonly IReadOnlyList<VolvoP2CemConfigOptionDefinition> Catalog =
        Array.AsReadOnly(
            VolvoP2ConfigOptionCatalog.Entries.Select(option =>
                new VolvoP2CemConfigOptionDefinition(
                    option.CanonicalId, option.DisplayName, option.Group.ToString(),
                    VolvoP2CemOptionProvenance.ThirdPartyAdvertisement,
                    option.Source,
                    option.AdvertisedEarliestModelYear,
                    option.ScopeLastModelYear,
                    option.SafetyOrComplianceReviewRequired,
                    null, null, null, null, NoVerifiedValues, NoVerifiedValues))
                .Concat(VolvoXc90OemCemMenuCatalog.Entries.Select(feature =>
                    new VolvoP2CemConfigOptionDefinition(
                        feature.ResearchId, feature.Name, "XC90 CEM service menu",
                        VolvoP2CemOptionProvenance.VolvoPublishedCemServiceMenu,
                        feature.PublicSource,
                        null, null,
                        feature.ConditionalEquipment,
                        null, null, null, null, NoVerifiedValues, NoVerifiedValues)))
                .ToArray());

    internal static IReadOnlyList<VolvoP2CemConfigOptionDefinition> Entries => Catalog;

    /// <summary>
    /// Exact canonical lookup. Unknown labels are not coerced to similar
    /// manufacturer or vendor names and user text is never reported.
    /// </summary>
    internal static VolvoP2CemConfigOptionDefinition? Find(string? canonicalId) =>
        canonicalId is { Length: > 0 and <= 128 }
            ? Catalog.FirstOrDefault(option =>
                string.Equals(option.CanonicalId, canonicalId, StringComparison.Ordinal))
            : null;
}
