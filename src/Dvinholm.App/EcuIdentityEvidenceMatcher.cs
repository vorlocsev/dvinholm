namespace Dvinholm.App;

/// <summary>
/// Read-only classification of an observed ECU identity. A verified identity
/// match is evidence for cataloging, NOT authorization to write, unlock or
/// program the module.
/// </summary>
internal enum EcuIdentityMatchKind
{
    Unknown,
    VerifiedReadOnlyIdentity,
    Ambiguous
}

/// <summary>
/// A future model-specific compatibility entry. Both identifying DID values
/// and the exact CAN addressing must be independently verified and sourced.
/// No Volvo P2/CEM signatures are bundled until that evidence exists.
/// </summary>
internal sealed record EcuIdentityEvidenceRule(
    string VariantId,
    string Platform,
    string ModuleName,
    uint RequestId,
    uint ResponseId,
    bool IsExtended,
    bool IsVerified,
    string EvidenceSource,
    IReadOnlyList<EcuDidReadback> RequiredIdentifiers);

internal sealed record EcuIdentityAssessment(
    EcuIdentityMatchKind Kind,
    string? VariantId,
    string Explanation);

/// <summary>
/// Match only exact, sourced, multi-DID ECU fingerprints from a completed
/// readback. Unknown or conflicting observations must fail closed. This class
/// sends no requests and never enables security access or configuration writes.
/// </summary>
internal static class EcuIdentityEvidenceMatcher
{
    internal static EcuIdentityAssessment Assess(
        EcuConfigurationReadSnapshot snapshot,
        IEnumerable<EcuIdentityEvidenceRule> rules)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rules);

        var unknown = new EcuIdentityAssessment(
            EcuIdentityMatchKind.Unknown, null,
            "No independently verified ECU identity signature matches this readback.");

        if (snapshot.Values is null || snapshot.Values.Count == 0)
            return unknown;

        var observed = new Dictionary<ushort, string>();
        foreach (var entry in snapshot.Values)
        {
            if (entry is null || !IsValidHex(entry.ValueHex) ||
                !observed.TryAdd(entry.DataIdentifier, entry.ValueHex))
                return unknown;
        }

        var matches = new List<EcuIdentityEvidenceRule>();
        foreach (var rule in rules)
        {
            if (!IsEligible(rule) ||
                rule.RequestId != snapshot.RequestId ||
                rule.ResponseId != snapshot.ResponseId ||
                rule.IsExtended != snapshot.IsExtended)
                continue;

            if (rule.RequiredIdentifiers.All(expected =>
                    observed.TryGetValue(expected.DataIdentifier, out var actual) &&
                    string.Equals(actual, expected.ValueHex, StringComparison.OrdinalIgnoreCase)))
                matches.Add(rule);
        }

        if (matches.Count == 1)
            return new EcuIdentityAssessment(
                EcuIdentityMatchKind.VerifiedReadOnlyIdentity,
                matches[0].VariantId,
                "An independently sourced ECU identity signature matches. Write access is not authorized.");

        if (matches.Count > 1)
            return new EcuIdentityAssessment(
                EcuIdentityMatchKind.Ambiguous, null,
                "Multiple verified identity signatures match; the ECU variant cannot be selected.");

        return unknown;
    }

    private static bool IsEligible(EcuIdentityEvidenceRule? rule)
    {
        if (rule is null || !rule.IsVerified ||
            string.IsNullOrWhiteSpace(rule.VariantId) ||
            string.IsNullOrWhiteSpace(rule.Platform) ||
            string.IsNullOrWhiteSpace(rule.ModuleName) ||
            string.IsNullOrWhiteSpace(rule.EvidenceSource) ||
            rule.RequiredIdentifiers is null ||
            rule.RequiredIdentifiers.Count is < 2 or > EcuConfigurationReadback.MaximumDataIdentifiers)
            return false;

        var seen = new HashSet<ushort>();
        foreach (var entry in rule.RequiredIdentifiers)
        {
            // VIN (F190) is vehicle-specific personal data; never use it as
            // the cross-vehicle identity signature for an ECU hardware variant.
            if (entry is null || entry.DataIdentifier == 0xF190 ||
                !IsValidHex(entry.ValueHex) || !seen.Add(entry.DataIdentifier))
                return false;
        }

        return true;
    }

    private static bool IsValidHex(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length is > 512 ||
            (value.Length & 1) != 0)
            return false;

        return value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');
    }
}
