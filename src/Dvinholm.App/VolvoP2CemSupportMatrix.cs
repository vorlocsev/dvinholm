namespace Dvinholm.App;

/// <summary>
/// Conservative, read-only ECU support classification for first-generation
/// XC90 on Volvo P2. The built-in production catalog stays EMPTY until
/// independently sourced ECU hardware/firmware and addressing are verified.
/// </summary>
internal enum VolvoP2CemSupportKind
{
    Unknown,
    VerifiedIdentityReadOnly,
    DocumentedReadOnlyDidSet,
    Ambiguous
}

/// <summary>
/// A model-year-scoped claim with an exact, independently sourced ECU identity
/// fingerprint. Year alone and a profile's 'verified' checkbox are NOT enough
/// for hardware identification. DID allowlists require separate provenance.
/// </summary>
internal sealed record VolvoP2CemSupportEntry(
    int ModelYearFrom,
    int ModelYearThrough,
    EcuIdentityEvidenceRule Identity,
    IReadOnlyCollection<ushort> DocumentedReadableDids,
    string ReadDidEvidenceSource);

internal sealed record VolvoP2CemSupportAssessment(
    VolvoP2CemSupportKind Kind,
    string? VariantId,
    IReadOnlyList<ushort> ObservedDocumentedDids,
    string Explanation)
{
    // A match to a supplied text/DID research rule does not authenticate
    // where a capture came from or prove it was a physical Volvo CEM.
    // The production catalog is empty and has no physical attestation path.
    internal bool HasAuthenticatedPhysicalCem => false;
    // Authorization, PIN acquisition and write APIs do not exist yet.
    // Never derive these capabilities from an identity match or a DID read.
    internal bool PinRetrievalSupported => false;
    internal bool ConfigurationWriteSupported => false;
}

/// <summary>
/// Matches an observed complete DID readback against explicitly supplied
/// evidence for an XC90 P2 CEM module, exact model year, 11/29-bit CAN IDs,
/// and multi-DID hardware/software identity. No transport requests occur.
/// An unknown or conflicting ECU always fails closed.
/// </summary>
internal static class VolvoP2CemSupportMatrix
{
    internal static IReadOnlyList<VolvoP2CemSupportEntry> ProductionEntries =>
        Array.Empty<VolvoP2CemSupportEntry>();

    internal static VolvoP2CemSupportAssessment Assess(
        int modelYear,
        EcuConfigurationReadSnapshot snapshot,
        IEnumerable<VolvoP2CemSupportEntry> evidence)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(evidence);

        var unknown = new VolvoP2CemSupportAssessment(
            VolvoP2CemSupportKind.Unknown, null, Array.Empty<ushort>(),
            "No verified XC90 P2/CEM identity and model-year evidence is available. " +
            "No PIN or configuration write support is implied.");

        // The low-level matcher is usable directly by offline callers,
        // not only through CaptureIdentityReview's checked UI path.
        // Reject unscoped years and captures without a real UTC timestamp
        // before a user-supplied synthetic signature could be "matched".
        if (modelYear <
                VolvoP2CemHsCanSpeedAdvisory.FirstDocumentedXc90ModelYear ||
            modelYear >
                VolvoP2CemHsCanSpeedAdvisory.LastFirstGenerationXc90ModelYear ||
            snapshot.CapturedAtUtc == default ||
            snapshot.CapturedAtUtc.Offset != TimeSpan.Zero ||
            snapshot.Values is null ||
            snapshot.Values.Count is < 1 or > EcuConfigurationReadback.MaximumDataIdentifiers)
            return unknown;

        // Scan no more than 129 entries even for an untrusted or infinite
        // enumerable. A single malformed/conflicting entry makes the entire
        // supplied evidence catalog ineligible instead of silently ignoring it.
        var supplied = evidence.Take(
            VolvoP2CemEvidenceCatalogValidator.MaximumEntries + 1).ToArray();
        if (VolvoP2CemEvidenceCatalogValidator.Validate(supplied).Count != 0)
            return unknown;

        var candidates = supplied
            .Where(entry => modelYear >= entry.ModelYearFrom &&
                            modelYear <= entry.ModelYearThrough)
            .ToArray();
        if (candidates.Length == 0)
            return unknown;

        // Fail closed on ambiguity instead of selecting the first variant.
        var identity = EcuIdentityEvidenceMatcher.Assess(
            snapshot, candidates.Select(entry => entry.Identity));

        if (identity.Kind == EcuIdentityMatchKind.Ambiguous)
            return new VolvoP2CemSupportAssessment(
                VolvoP2CemSupportKind.Ambiguous, null, Array.Empty<ushort>(),
                "Conflicting XC90 P2/CEM identity evidence: no variant selected.");
        if (identity.Kind != EcuIdentityMatchKind.VerifiedReadOnlyIdentity ||
            identity.VariantId is null)
            return unknown;

        // Distinct rules can use the same textual VariantId; never trust a
        // first-match lookup unless it is uniquely attributable to evidence.
        var matched = candidates.Where(entry =>
            string.Equals(entry.Identity.VariantId, identity.VariantId,
                StringComparison.Ordinal)).ToArray();
        if (matched.Length != 1)
            return new VolvoP2CemSupportAssessment(
                VolvoP2CemSupportKind.Ambiguous, null, Array.Empty<ushort>(),
                "Duplicate XC90 P2/CEM variant evidence: no variant selected.");

        var selected = matched[0];
        var observed = snapshot.Values.Select(v => v.DataIdentifier).ToHashSet();
        var dids = selected.DocumentedReadableDids
            .Where(observed.Contains).Order().ToArray();

        if (dids.Length == 0)
            return new VolvoP2CemSupportAssessment(
                VolvoP2CemSupportKind.VerifiedIdentityReadOnly,
                identity.VariantId, Array.Empty<ushort>(),
                "Verified read-only ECU identity only. No separately documented configuration DIDs observed.");

        return new VolvoP2CemSupportAssessment(
            VolvoP2CemSupportKind.DocumentedReadOnlyDidSet,
            identity.VariantId, Array.AsReadOnly(dids),
            "Verified, sourced read-only ECU identity with documented observed DIDs. " +
            "These are opaque values, not decoded configuration or a restorable backup.");
    }

}
