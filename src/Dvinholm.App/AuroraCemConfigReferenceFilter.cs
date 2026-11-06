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

    /// <summary>
    /// Russian UI searches include the translated display labels without
    /// changing canonical IDs or weakening the original evidence/category
    /// filters. Original English queries remain accepted in both locales.
    /// </summary>
    internal static IReadOnlyList<VolvoP2CemConfigOptionDefinition> FindLocalized(
        string? query, string? category, VolvoP2CemCatalogEvidenceTier tier)
    {
        // Validate all input through the original strict search first.
        var original = Find(query, category, tier);
        if (string.IsNullOrWhiteSpace(query)) return original;

        var terms = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var allowed = Find(null, category, tier);
        return Array.AsReadOnly(allowed.Where(option =>
        {
            var translatedName = AuroraCemCatalogLocalization.OptionLabel(option, false);
            var translatedGroup = AuroraCemCatalogLocalization.CategoryLabel(
                option.Category, false);
            return terms.All(term =>
                option.CanonicalId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                option.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                option.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                translatedName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                translatedGroup.Contains(term, StringComparison.OrdinalIgnoreCase));
        }).Take(MaximumRows).ToArray());
    }

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
