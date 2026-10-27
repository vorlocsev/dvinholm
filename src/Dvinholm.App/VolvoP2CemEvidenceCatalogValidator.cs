namespace Dvinholm.App;

/// <summary>
/// Structural checks for candidate, independently researched XC90 P2 CEM
/// evidence. Passing validation does NOT prove a source to be truthful, grant
/// authorization or mean that any physical ECU is supported.
/// </summary>
internal enum VolvoP2CemEvidenceIssueCode
{
    TooManyEntries,
    InvalidScope,
    UnverifiedIdentity,
    InvalidCanAddressing,
    InvalidFingerprint,
    InvalidDidMapping,
    ConflictingVariantYears
}

internal sealed record VolvoP2CemEvidenceIssue(
    VolvoP2CemEvidenceIssueCode Code,
    int EntryIndex);

internal static class VolvoP2CemEvidenceCatalogValidator
{
    internal const int MaximumEntries = 128;

    internal static IReadOnlyList<VolvoP2CemEvidenceIssue> Validate(
        IEnumerable<VolvoP2CemSupportEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var issues = new List<VolvoP2CemEvidenceIssue>();
        var accepted = new List<(VolvoP2CemSupportEntry Entry, int Index)>();
        var index = 0;

        foreach (var entry in entries)
        {
            if (index >= MaximumEntries)
            {
                issues.Add(new(VolvoP2CemEvidenceIssueCode.TooManyEntries, index));
                break; // Never enumerate an unlimited imported catalog.
            }

            if (entry is null || entry.ModelYearFrom < 1900 ||
                entry.ModelYearThrough > 2100 ||
                entry.ModelYearFrom > entry.ModelYearThrough)
            {
                issues.Add(new(VolvoP2CemEvidenceIssueCode.InvalidScope, index++));
                continue;
            }

            var rule = entry.Identity;
            if (rule is null || !rule.IsVerified ||
                !string.Equals(rule.Platform, "P2", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(rule.ModuleName, "CEM", StringComparison.OrdinalIgnoreCase) ||
                !ValidText(rule.VariantId, 120) ||
                !ValidText(rule.EvidenceSource, 512))
            {
                issues.Add(new(VolvoP2CemEvidenceIssueCode.UnverifiedIdentity, index++));
                continue;
            }

            var maximumCanId = rule.IsExtended ? 0x1FFFFFFFu : 0x7FFu;
            if (rule.RequestId > maximumCanId ||
                rule.ResponseId > maximumCanId ||
                rule.RequestId == rule.ResponseId)
            {
                issues.Add(new(VolvoP2CemEvidenceIssueCode.InvalidCanAddressing, index++));
                continue;
            }

            if (rule.RequiredIdentifiers is null ||
                rule.RequiredIdentifiers.Count is < 2 or > EcuConfigurationReadback.MaximumDataIdentifiers ||
                !HasDistinctValidFingerprint(rule.RequiredIdentifiers))
            {
                issues.Add(new(VolvoP2CemEvidenceIssueCode.InvalidFingerprint, index++));
                continue;
            }

            if (entry.DocumentedReadableDids is null ||
                entry.DocumentedReadableDids.Count > EcuConfigurationReadback.MaximumDataIdentifiers ||
                entry.DocumentedReadableDids.Contains((ushort)0xF190) ||
                entry.DocumentedReadableDids.Distinct().Count() != entry.DocumentedReadableDids.Count ||
                (entry.DocumentedReadableDids.Count > 0 &&
                 !ValidText(entry.ReadDidEvidenceSource, 512)))
            {
                issues.Add(new(VolvoP2CemEvidenceIssueCode.InvalidDidMapping, index++));
                continue;
            }

            // Same textual variant in overlapping years has conflicting
            // catalog provenance. Different revisions sharing CAN IDs are
            // allowed, but an observed multi-DID match still must be unique.
            if (accepted.Any(existing =>
                    string.Equals(existing.Entry.Identity.VariantId, rule.VariantId,
                        StringComparison.OrdinalIgnoreCase) &&
                    existing.Entry.ModelYearFrom <= entry.ModelYearThrough &&
                    entry.ModelYearFrom <= existing.Entry.ModelYearThrough))
            {
                issues.Add(new(VolvoP2CemEvidenceIssueCode.ConflictingVariantYears, index++));
                continue;
            }

            accepted.Add((entry, index));
            index++;
        }

        return Array.AsReadOnly(issues.ToArray());
    }

    private static bool HasDistinctValidFingerprint(
        IReadOnlyList<EcuDidReadback> identifiers)
    {
        var seen = new HashSet<ushort>();
        foreach (var observation in identifiers)
        {
            if (observation is null || observation.DataIdentifier == 0xF190 ||
                !seen.Add(observation.DataIdentifier) ||
                !IsHexBytes(observation.ValueHex))
                return false;
        }
        return true;
    }

    private static bool IsHexBytes(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 512 &&
        (value.Length & 1) == 0 &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');

    private static bool ValidText(string? value, int maxCharacters) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxCharacters &&
        !value.Any(char.IsControl);
}
