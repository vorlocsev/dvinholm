namespace Dvinholm.App;

/// <summary>
/// Searches the user-imported ECU profile catalog without making assumptions
/// about the target vehicle. "Verified" is a profile metadata flag supplied
/// by the catalog author and is not proof of vehicle compatibility.
/// </summary>
internal static class EcuProfileCatalogSearch
{
    internal static IReadOnlyList<EcuProfile> Filter(
        EcuProfileCatalog catalog,
        string? searchText,
        bool markedVerifiedOnly = false)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var query = (searchText ?? string.Empty).Trim();
        // Accept the common 0x prefix for CAN IDs and DID hexadecimal text.
        if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            query = query[2..];

        return catalog.All
            .Where(profile => (!markedVerifiedOnly || profile.IsVerified)
                && (query.Length == 0 || Matches(profile, query)))
            .ToArray();
    }

    private static bool Matches(EcuProfile profile, string query)
    {
        if (profile.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            profile.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            profile.Source.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        // Match what the ECU details actually display. Hexadecimal searches
        // are supported for both standard and extended CAN addressing.
        var width = profile.IsExtended ? "X8" : "X3";
        if (profile.RequestId.ToString(width).Contains(query, StringComparison.OrdinalIgnoreCase) ||
            profile.ResponseId.ToString(width).Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        return profile.ReadableDataIdentifiers.Any(did =>
            did.ToString("X4").Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
