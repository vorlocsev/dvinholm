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

        if (modelYear is < 1900 or > 2100 || snapshot.Values is null ||
            snapshot.Values.Count is < 1 or > EcuConfigurationReadback.MaximumDataIdentifiers)
            return unknown;

        var candidates = evidence
            .Where(entry => IsEligible(entry, modelYear))
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

    private static bool IsEligible(VolvoP2CemSupportEntry? entry, int modelYear)
    {
        if (entry is null || entry.Identity is not { } identity ||
            modelYear < entry.ModelYearFrom || modelYear > entry.ModelYearThrough ||
            entry.ModelYearFrom > entry.ModelYearThrough ||
            !identity.IsVerified ||
            !string.Equals(identity.Platform, "P2", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(identity.ModuleName, "CEM", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(identity.VariantId) ||
            string.IsNullOrWhiteSpace(identity.EvidenceSource) ||
            entry.DocumentedReadableDids is null ||
            entry.DocumentedReadableDids.Count > EcuConfigurationReadback.MaximumDataIdentifiers)
            return false;

        if (entry.DocumentedReadableDids.Count == 0)
            return true; // An identity may be sourced before data mapping is.

        return !string.IsNullOrWhiteSpace(entry.ReadDidEvidenceSource) &&
               entry.DocumentedReadableDids.Distinct().Count() ==
                   entry.DocumentedReadableDids.Count &&
               !entry.DocumentedReadableDids.Contains(0xF190);
    }
}
