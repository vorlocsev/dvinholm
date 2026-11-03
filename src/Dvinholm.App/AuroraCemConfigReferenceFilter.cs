namespace Dvinholm.App;

/// <summary>
/// Pure read-only projection for the approved Aurora CEM browser. Source tier
/// and category are applied to the same evidence manifest used by the older
/// offline research viewer. Search input is never logged or transmitted.
/// No J2534, VIN, PIN, configuration value, or firmware mapping is involved.
/// </summary>
internal static class AuroraCemConfigReferenceFilter
{
    internal const int MaximumRows = 128;

    internal static IReadOnlyList<VolvoP2CemConfigOptionDefinition> Find(
        string? query, string? category, VolvoP2CemCatalogEvidenceTier tier)
    {
        if (category is { Length: > 80 } ||
            category is not null && category.Any(char.IsControl))
            throw new InvalidDataException("Invalid CEM reference category.");

        // The established offline search validates the query length, control
        // characters and tier, and requires every search token to match.
        var ids = VolvoP2CemCatalogSearch.Find(query, tier)
            .Select(item => item.ResearchId)
            .ToHashSet(StringComparer.Ordinal);

        return Array.AsReadOnly(VolvoP2CemConfigOptionManifest.Entries
            .Where(item => ids.Contains(item.CanonicalId))
            .Where(item => string.IsNullOrWhiteSpace(category) ||
                string.Equals(item.Category, category, StringComparison.Ordinal))
            .Take(MaximumRows)
            .ToArray());
    }
}
